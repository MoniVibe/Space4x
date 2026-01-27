#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [CreateAssetMenu(menuName = "PureDOTS/Meta Registries/Faction Profile", fileName = "FactionProfile")]
    public sealed class FactionProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        [Min(0)] public ushort factionId = 1;
        public string displayName = "Faction";
        public FactionType factionType = FactionType.Neutral;

        [Header("Initial State")]
        [Min(0f)] public float resourceStockpile;
        [Min(0)] public int populationCount;
        [Min(0)] public int territoryCellCount;
        public Vector3 territoryCenter = Vector3.zero;
        public DiplomaticStatusFlags diplomaticStatus = DiplomaticStatusFlags.None;

        public void CopyTo(FactionAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            authoring.FactionId = factionId;
            authoring.FactionName = displayName;
            authoring.FactionType = factionType;
            authoring.ResourceStockpile = resourceStockpile;
            authoring.PopulationCount = populationCount;
            authoring.TerritoryCellCount = territoryCellCount;
            authoring.TerritoryCenter = territoryCenter;
            authoring.DiplomaticStatus = diplomaticStatus;
        }
    }
}
#endif


