#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [DisallowMultipleComponent]
    public sealed class FactionAuthoring : MonoBehaviour
    {
        [SerializeField]
        private FactionProfileAsset profile;

        [SerializeField]
        private bool applyProfileOnValidate = true;

        [Header("Identity")]
        [SerializeField] private ushort factionId = 1;
        [SerializeField] private string factionName = "Faction";
        [SerializeField] private FactionType factionType = FactionType.Neutral;

        [Header("Initial State")]
        [SerializeField] private float resourceStockpile;
        [SerializeField] private int populationCount;
        [SerializeField] private int territoryCellCount;
        [SerializeField] private Vector3 territoryCenter;
        [SerializeField] private DiplomaticStatusFlags diplomaticStatus = DiplomaticStatusFlags.None;

        [Header("Options")]
        [Tooltip("Automatically add SpatialIndexedTag so the entity participates in spatial queries.")]
        public bool addSpatialIndexedTag = true;

        public ushort FactionId
        {
            get => factionId;
            set => factionId = value;
        }

        public string FactionName
        {
            get => factionName;
            set => factionName = value;
        }

        public FactionType FactionType
        {
            get => factionType;
            set => factionType = value;
        }

        public float ResourceStockpile
        {
            get => resourceStockpile;
            set => resourceStockpile = value;
        }

        public int PopulationCount
        {
            get => populationCount;
            set => populationCount = value;
        }

        public int TerritoryCellCount
        {
            get => territoryCellCount;
            set => territoryCellCount = value;
        }

        public Vector3 TerritoryCenter
        {
            get => territoryCenter;
            set => territoryCenter = value;
        }

        public DiplomaticStatusFlags DiplomaticStatus
        {
            get => diplomaticStatus;
            set => diplomaticStatus = value;
        }

        private void OnValidate()
        {
            if (!applyProfileOnValidate || profile == null)
            {
                return;
            }

            profile.CopyTo(this);
        }

        internal (FactionId id, FactionState state) BuildComponents()
        {
            var id = new FactionId
            {
                Value = factionId,
                Name = new FixedString64Bytes(factionName ?? string.Empty),
                Type = factionType
            };

            var state = new FactionState
            {
                ResourceStockpile = resourceStockpile,
                PopulationCount = populationCount,
                TerritoryCellCount = territoryCellCount,
                DiplomaticStatus = diplomaticStatus,
                TerritoryCenter = (float3)territoryCenter
            };

            return (id, state);
        }
    }

    public sealed class FactionAuthoringBaker : Baker<FactionAuthoring>
    {
        public override void Bake(FactionAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var (id, state) = authoring.BuildComponents();

            AddComponent(entity, id);
            AddComponent(entity, state);

            if (authoring.addSpatialIndexedTag)
            {
                AddComponent<SpatialIndexedTag>(entity);
            }
        }
    }
}
#endif

