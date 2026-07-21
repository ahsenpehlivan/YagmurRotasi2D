using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Core2D;
using YagmurRotasi2D.Data2D;
using YagmurRotasi2D.Gameplay2D;

/// <summary>
/// Editor-only constraint solver (Part M) that counts how many rotation
/// assignments of a level's already-placed pipes (position/type fixed by the
/// generator - only rotationIndex is searched over) make it solvable, up to a
/// maximum of 2, so the generator can reject non-unique candidates. Every
/// complete assignment is validated by the real, unchanged FlowSolver2D - no
/// approximation of leak/reachability rules is ever used for acceptance, only
/// for the one pruning rule below (which is unconditionally safe regardless
/// of reachability, since it concerns the single edge that is ALWAYS
/// reachable by construction: Source's fixed first hop).
///
/// Variable ordering is the fixed order pipes are supplied in (the caller
/// passes them in the graph builder's own BFS-from-source discovery order) -
/// a real MRV (dynamic minimum-remaining-values) implementation was not
/// attempted; this is a deliberate, documented simplification. Bounded by a
/// complete-assignment count (not wall-clock time, for determinism) so a
/// large candidate cannot freeze the Editor - if the budget is exhausted
/// before reaching a definitive answer, the result is Inconclusive and the
/// generator must treat that exactly like any other rejected attempt (retry
/// with a different seed), never assume uniqueness.
/// </summary>
public static class CampaignUniquenessSolver2D
{
    public enum UniquenessOutcome
    {
        Zero,
        One,
        TwoOrMore,
        Inconclusive
    }

    public static UniquenessOutcome CountSolutions(
        Vector2Int sourceCell,
        Direction2D sourceOutputDirection,
        Vector2Int targetCell,
        Direction2D targetInputDirection,
        IReadOnlyList<PipeSpawnData2D> pipesInSearchOrder,
        int maxCompleteAssignments,
        out int solutionsFound)
    {
        var spawned = new List<GameObject>();
        int found = 0;
        int assignmentsChecked = 0;
        bool budgetExceeded = false;

        try
        {
            BoardManager2D board = BranchingSolverTestRunner.CreateBoard(spawned);
            FlowSolver2D solver = BranchingSolverTestRunner.CreateSolver(board, spawned);

            var pipeInstances = new PipeTile2D[pipesInSearchOrder.Count];
            for (int i = 0; i < pipesInSearchOrder.Count; i++)
            {
                PipeSpawnData2D spec = pipesInSearchOrder[i];
                pipeInstances[i] = BranchingSolverTestRunner.CreatePipe(board, spawned, spec.pipeType, spec.gridPos, 0);
            }

            // The one unconditionally-safe pruning rule: whichever pipe sits
            // at sourceCell + sourceOutputDirection is reachable by definition
            // (zero intermediate pipes required) - any rotation that does not
            // open toward Source can never lead to a solution, regardless of
            // every other pipe's rotation, so skipping it early is always
            // correct (not an approximation).
            Vector2Int firstHopCell = sourceCell + sourceOutputDirection.ToVector();
            Direction2D requiredFirstHopOpening = sourceOutputDirection.Opposite();

            void Recurse(int index)
            {
                if (found >= 2 || budgetExceeded)
                {
                    return;
                }

                if (index == pipesInSearchOrder.Count)
                {
                    assignmentsChecked++;
                    if (assignmentsChecked > maxCompleteAssignments)
                    {
                        budgetExceeded = true;
                        return;
                    }

                    FlowSolveResult2D result = solver.Solve(sourceCell, sourceOutputDirection, targetCell, targetInputDirection);
                    if (result.IsSuccess)
                    {
                        found++;
                    }
                    return;
                }

                PipeSpawnData2D spec = pipesInSearchOrder[index];
                int rotationCount = spec.pipeType == PipeType2D.Cross ? 1 : 4;

                for (int r = 0; r < rotationCount; r++)
                {
                    if (found >= 2 || budgetExceeded)
                    {
                        return;
                    }

                    pipeInstances[index].Initialize(spec.pipeType, r, spec.gridPos);

                    if (spec.gridPos == firstHopCell && !pipeInstances[index].HasOpening(requiredFirstHopOpening))
                    {
                        continue;
                    }

                    Recurse(index + 1);
                }
            }

            Recurse(0);
        }
        finally
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        solutionsFound = found;

        if (budgetExceeded)
        {
            return UniquenessOutcome.Inconclusive;
        }

        if (found == 0) return UniquenessOutcome.Zero;
        if (found == 1) return UniquenessOutcome.One;
        return UniquenessOutcome.TwoOrMore;
    }
}
