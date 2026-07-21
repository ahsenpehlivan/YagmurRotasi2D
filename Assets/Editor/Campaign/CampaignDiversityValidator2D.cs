using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using YagmurRotasi2D.Campaign2D;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Phase 8 Fast-Track QA: the existing CampaignContentHash2D hash is a
/// change-detection hash (it includes levelNumber/seed/generatorVersion/start
/// rotations - see its own doc comment), NOT a topology-diversity check, so
/// two levels with genuinely identical solved layouts can still have
/// different stored hashes, and it says nothing about how visually similar
/// (but not identical) two levels are. This command builds a SEPARATE,
/// intentionally narrower "solved topology fingerprint" per level (grid
/// size, Source/Target placement, and every pipe's coordinate/type/solved
/// canonical port mask only) to find exact duplicates, plus a same-grid-size
/// pairwise similarity score to find near-duplicates. Read-only: never
/// modifies or regenerates any asset.
/// </summary>
public static class CampaignDiversityValidator2D
{
    private const float NearDuplicateThreshold = 0.85f;

    // Weights for the near-duplicate similarity score - deliberately
    // documented and tunable in one place. Cell/edge overlap dominate (they
    // capture the actual visual layout shape); Source/Target placement
    // contributes least since it is fixed by the graph builder's corner
    // convention for every generated level of a given grid size, so it
    // rarely discriminates between same-size levels on its own.
    private const float CellWeight = 0.30f;
    private const float TypeWeight = 0.25f;
    private const float EdgeWeight = 0.35f;
    private const float SourceTargetWeight = 0.10f;

    private sealed class LevelTopology
    {
        public int LevelNumber;
        public int GridWidth;
        public int GridHeight;
        public Vector2Int SourceCell;
        public Direction2D SourceDirection;
        public Vector2Int TargetCell;
        public Direction2D TargetDirection;
        public HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
        public Dictionary<Vector2Int, PipeType2D> TypeByCell = new Dictionary<Vector2Int, PipeType2D>();
        public HashSet<string> Edges = new HashSet<string>();
        public string Fingerprint;
        public string FingerprintHash;
    }

    [MenuItem("YagmurRotasi2D/Phase 8/Validate Campaign Diversity")]
    public static void ValidateCampaignDiversity()
    {
        CampaignLevelCatalog2D catalog = AssetDatabase.LoadAssetAtPath<CampaignLevelCatalog2D>(CampaignAssetIO2D.CatalogAssetPath);
        if (catalog == null || catalog.levels == null || catalog.levels.Count == 0)
        {
            Debug.LogError("CampaignDiversityValidator2D: No catalog found (or catalog is empty) at " + CampaignAssetIO2D.CatalogAssetPath + ".");
            return;
        }

        var topologies = new List<LevelTopology>();
        for (int i = 0; i < catalog.levels.Count; i++)
        {
            CampaignLevelDefinition2D def = catalog.levels[i];
            if (def == null || def.pipes == null || def.pipes.Count == 0)
            {
                continue;
            }

            topologies.Add(BuildTopology(def));
        }

        var lines = new List<string>
        {
            $"Campaign diversity report - {topologies.Count} level(s) inspected."
        };

        ReportExactDuplicates(topologies, lines);
        ReportNearDuplicates(topologies, lines);

        Debug.Log(string.Join("\n", lines));
    }

    // ---------------- Fingerprint / topology construction ----------------

    /// <summary>
    /// Exact solved-topology fingerprint: grid width/height, Source
    /// coordinate+direction, Target coordinate+direction, and every pipe's
    /// coordinate/type/solved canonical port mask (via PipeTile2D.GetPortMask -
    /// logical open-direction sets, not raw solvedRotationIndex, so Straight's
    /// two-fold symmetry never produces a spurious fingerprint difference),
    /// sorted by stable grid-coordinate order (x then y). Deliberately EXCLUDES
    /// levelNumber, displayName, deterministicSeed, generatorVersion,
    /// startRotationIndex, minimumRequiredTaps and the existing contentHash -
    /// none of those affect solved topology.
    /// </summary>
    private static LevelTopology BuildTopology(CampaignLevelDefinition2D def)
    {
        var topo = new LevelTopology
        {
            LevelNumber = def.levelNumber,
            GridWidth = def.gridWidth,
            GridHeight = def.gridHeight,
            SourceCell = def.sourceCell,
            SourceDirection = def.sourceOutputDirection,
            TargetCell = def.targetCell,
            TargetDirection = def.targetEntryDirection
        };

        var maskByCell = new Dictionary<Vector2Int, int>();
        foreach (PipeSpawnData2D pipe in def.pipes)
        {
            topo.Cells.Add(pipe.gridPos);
            topo.TypeByCell[pipe.gridPos] = pipe.pipeType;
            maskByCell[pipe.gridPos] = PipeTile2D.GetPortMask(pipe.pipeType, pipe.solvedRotationIndex);
        }

        // Solved connection edges: for every open port on every pipe cell, the
        // neighbor in that direction is always either another pipe cell,
        // Source, or Target (the generator never accepts a dangling port) -
        // Source/Target are represented as fixed node ids so an edge directly
        // into either is still part of the comparable graph shape, not lost.
        foreach (KeyValuePair<Vector2Int, int> kv in maskByCell)
        {
            Vector2Int cell = kv.Key;
            int mask = kv.Value;

            foreach (Direction2D dir in new[] { Direction2D.Up, Direction2D.Right, Direction2D.Down, Direction2D.Left })
            {
                if ((mask & (1 << (int)dir)) == 0)
                {
                    continue;
                }

                Vector2Int neighbor = cell + dir.ToVector();
                string a = NodeId(cell, topo);
                string b = NodeId(neighbor, topo);
                string edge = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
                topo.Edges.Add(edge);
            }
        }

        var sortedPipes = new List<PipeSpawnData2D>(def.pipes);
        sortedPipes.Sort((x, y) => x.gridPos.x != y.gridPos.x ? x.gridPos.x.CompareTo(y.gridPos.x) : x.gridPos.y.CompareTo(y.gridPos.y));

        var sb = new StringBuilder();
        sb.Append(def.gridWidth).Append('x').Append(def.gridHeight).Append('|');
        sb.Append("S(").Append(def.sourceCell.x).Append(',').Append(def.sourceCell.y).Append(")>").Append(def.sourceOutputDirection).Append('|');
        sb.Append("T(").Append(def.targetCell.x).Append(',').Append(def.targetCell.y).Append(")<").Append(def.targetEntryDirection).Append('|');
        foreach (PipeSpawnData2D pipe in sortedPipes)
        {
            int mask = PipeTile2D.GetPortMask(pipe.pipeType, pipe.solvedRotationIndex);
            sb.Append('(').Append(pipe.gridPos.x).Append(',').Append(pipe.gridPos.y).Append(')').Append(':').Append(pipe.pipeType).Append(':').Append(mask).Append(';');
        }

        topo.Fingerprint = sb.ToString();
        topo.FingerprintHash = ComputeSha256(topo.Fingerprint);

        return topo;
    }

    private static string NodeId(Vector2Int cell, LevelTopology topo)
    {
        if (cell == topo.SourceCell) return "SRC";
        if (cell == topo.TargetCell) return "TGT";
        return $"{cell.x},{cell.y}";
    }

    private static string ComputeSha256(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }

    // ---------------- Exact duplicate groups ----------------

    private static void ReportExactDuplicates(List<LevelTopology> topologies, List<string> lines)
    {
        var byHash = topologies.GroupBy(t => t.FingerprintHash).Where(g => g.Count() > 1).ToList();

        lines.Add("");
        lines.Add("=== Exact Duplicate Topology Groups ===");

        if (byHash.Count == 0)
        {
            lines.Add("None found - every level's solved topology fingerprint is unique.");
            return;
        }

        foreach (var group in byHash.OrderBy(g => g.Min(t => t.LevelNumber)))
        {
            List<int> levelNumbers = group.Select(t => t.LevelNumber).OrderBy(n => n).ToList();
            lines.Add($"DUPLICATE GROUP: Levels [{string.Join(", ", levelNumbers)}] share an IDENTICAL solved topology " +
                $"(fingerprintHash={group.Key.Substring(0, 12)}...).");
        }
    }

    // ---------------- Near-duplicate similarity ----------------

    private static void ReportNearDuplicates(List<LevelTopology> topologies, List<string> lines)
    {
        var pairs = new List<(int levelA, int levelB, float score, float cellSim, float typeSim, float edgeSim, float stSim)>();

        var byGridSize = topologies.GroupBy(t => (t.GridWidth, t.GridHeight));
        foreach (var group in byGridSize)
        {
            List<LevelTopology> list = group.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    float score = ComputeSimilarity(list[i], list[j], out float cellSim, out float typeSim, out float edgeSim, out float stSim);
                    if (score >= NearDuplicateThreshold)
                    {
                        pairs.Add((list[i].LevelNumber, list[j].LevelNumber, score, cellSim, typeSim, edgeSim, stSim));
                    }
                }
            }
        }

        lines.Add("");
        lines.Add($"=== Near-Duplicate Pairs (same grid size, similarity >= {NearDuplicateThreshold:0.00}) ===");

        if (pairs.Count == 0)
        {
            lines.Add("None found.");
            return;
        }

        pairs.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var pair in pairs)
        {
            lines.Add($"SIMILAR: Level {pair.levelA} <-> Level {pair.levelB} - score={pair.score:0.000} " +
                $"(cells={pair.cellSim:0.000}, types={pair.typeSim:0.000}, edges={pair.edgeSim:0.000}, sourceTarget={pair.stSim:0.000}).");
        }
    }

    /// <summary>
    /// Weighted similarity in [0,1] between two same-grid-size levels:
    /// - cell overlap: Jaccard(occupied pipe cells)
    /// - type overlap: fraction of the cell UNION whose pipe type also
    ///   matches within the shared cells (so both missing cells AND type
    ///   mismatches within shared cells are penalized)
    /// - edge overlap: Jaccard(solved connection edges, including Source/Target anchors)
    /// - Source/Target placement: fraction of {sourceCell, sourceDir, targetCell, targetDir} that match exactly
    /// </summary>
    private static float ComputeSimilarity(LevelTopology a, LevelTopology b, out float cellSim, out float typeSim, out float edgeSim, out float sourceTargetSim)
    {
        var cellUnion = new HashSet<Vector2Int>(a.Cells);
        cellUnion.UnionWith(b.Cells);
        var cellIntersect = new HashSet<Vector2Int>(a.Cells);
        cellIntersect.IntersectWith(b.Cells);

        cellSim = cellUnion.Count == 0 ? 1f : (float)cellIntersect.Count / cellUnion.Count;

        int typeMatches = 0;
        foreach (Vector2Int cell in cellIntersect)
        {
            if (a.TypeByCell[cell] == b.TypeByCell[cell])
            {
                typeMatches++;
            }
        }
        typeSim = cellUnion.Count == 0 ? 1f : (float)typeMatches / cellUnion.Count;

        var edgeUnion = new HashSet<string>(a.Edges);
        edgeUnion.UnionWith(b.Edges);
        var edgeIntersect = new HashSet<string>(a.Edges);
        edgeIntersect.IntersectWith(b.Edges);

        edgeSim = edgeUnion.Count == 0 ? 1f : (float)edgeIntersect.Count / edgeUnion.Count;

        int stMatches = 0;
        if (a.SourceCell == b.SourceCell) stMatches++;
        if (a.SourceDirection == b.SourceDirection) stMatches++;
        if (a.TargetCell == b.TargetCell) stMatches++;
        if (a.TargetDirection == b.TargetDirection) stMatches++;
        sourceTargetSim = stMatches / 4f;

        return CellWeight * cellSim + TypeWeight * typeSim + EdgeWeight * edgeSim + SourceTargetWeight * sourceTargetSim;
    }
}
