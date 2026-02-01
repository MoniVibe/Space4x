using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures a baseline individual profile catalog exists for normalization and authoring fallbacks.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XIndividualProfileCatalogBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<IndividualProfileCatalogSingleton>(out _))
            {
                state.Enabled = false;
                return;
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref builder.ConstructRoot<IndividualProfileCatalogBlob>();
            var profiles = builder.Allocate(ref catalogBlob.Profiles, 1);

            var outlooks = new FixedList64Bytes<OutlookWeight>();
            outlooks.Add(new OutlookWeight { OutlookId = OutlookId.Neutral, Weight = (half)0.15f });

            profiles[0] = new IndividualProfileTemplate
            {
                Id = new FixedString64Bytes("baseline"),
                Alignment = AlignmentTriplet.FromFloats(0f, 0f, 0f),
                Behavior = BehaviorDisposition.FromValues(0.7f, 0.6f, 0.65f, 0.45f, 0.4f, 0.6f),
                Stats = new IndividualStats
                {
                    Command = (half)65f,
                    Tactics = (half)60f,
                    Logistics = (half)60f,
                    Diplomacy = (half)55f,
                    Engineering = (half)50f,
                    Resolve = (half)60f
                },
                Physique = new PhysiqueFinesseWill
                {
                    Physique = (half)50f,
                    Finesse = (half)50f,
                    Will = (half)50f,
                    PhysiqueInclination = 5,
                    FinesseInclination = 5,
                    WillInclination = 5,
                    GeneralXP = 0f
                },
                Capacities = new DerivedCapacities
                {
                    Sight = 1f,
                    Manipulation = 1f,
                    Consciousness = 1f,
                    ReactionTime = 1f,
                    Boarding = 1f
                },
                Personality = PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f),
                Patriotism = PatriotismProfile.Default(),
                MoraleBaseline = 0f,
                MoraleDriftRate = 0.01f,
                BehaviorExplicit = 1,
                MoraleExplicit = 0,
                Reserved0 = 0,
                Reserved1 = 0,
                Outlooks = outlooks
            };

            var blobAsset = builder.CreateBlobAssetReference<IndividualProfileCatalogBlob>(Allocator.Persistent);

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new IndividualProfileCatalogSingleton
            {
                Catalog = blobAsset,
                DefaultProfileId = new FixedString64Bytes("baseline")
            });

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var catalog in SystemAPI.Query<RefRW<IndividualProfileCatalogSingleton>>())
            {
                if (catalog.ValueRO.Catalog.IsCreated)
                {
                    catalog.ValueRO.Catalog.Dispose();
                    catalog.ValueRW.Catalog = default;
                }
            }
        }
    }
}
