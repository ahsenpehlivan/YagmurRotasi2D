using System.Collections.Generic;
using UnityEngine;
using YagmurRotasi2D.Data2D;

namespace YagmurRotasi2D.Campaign2D
{
    /// <summary>
    /// Ordered list of production campaign level assets. LevelManager2D
    /// prefers this catalog when assigned (Phase 8A) and falls back to the
    /// original hardcoded BuildLevels() only if no catalog is wired - see
    /// LevelManager2D.ResolveLevels(). The catalog, once validated, is the
    /// single authoritative source; BuildLevels() is not meant to be edited
    /// further after a successful migration.
    /// </summary>
    [CreateAssetMenu(fileName = "CampaignLevelCatalog2D", menuName = "YagmurRotasi2D/Campaign Level Catalog")]
    public class CampaignLevelCatalog2D : ScriptableObject
    {
        public int catalogVersion = 1;
        public List<CampaignLevelDefinition2D> levels = new List<CampaignLevelDefinition2D>();

        public List<LevelData2D> ToLevelDataList()
        {
            var result = new List<LevelData2D>(levels.Count);
            foreach (CampaignLevelDefinition2D definition in levels)
            {
                if (definition != null)
                {
                    result.Add(definition.ToLevelData());
                }
            }
            return result;
        }
    }
}
