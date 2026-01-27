using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Initializes identity components (Alignment, Outlook, Personality, Might/Magic) for new entities.
    /// Sets random or seeded values based on entity type and context.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct IdentityInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EntityAlignment>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var rng = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime);

            // Initialize entities missing identity components
            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<EntityAlignment>>()
                .WithNone<EntityOutlook, PersonalityAxes, MightMagicAffinity>()
                .WithEntityAccess())
            {
                // Initialize Outlook (random selection from available types)
                var outlook = new EntityOutlook
                {
                    Primary = SelectRandomOutlook(ref rng),
                    Secondary = SelectRandomOutlook(ref rng),
                    Tertiary = SelectRandomOutlook(ref rng)
                };
                ecb.AddComponent(entity, outlook);

                // Initialize Personality (random values in reasonable range)
                var personality = new PersonalityAxes
                {
                    VengefulForgiving = rng.NextFloat(-80f, 80f),
                    CravenBold = rng.NextFloat(-80f, 80f)
                };
                ecb.AddComponent(entity, personality);

                // Initialize Might/Magic Affinity (default to middle range, moderate strength)
                var affinity = new MightMagicAffinity
                {
                    Axis = rng.NextFloat(-30f, 30f), // Default hybrid
                    Strength = rng.NextFloat(0.3f, 0.7f) // Moderate commitment
                };
                ecb.AddComponent(entity, affinity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static OutlookType SelectRandomOutlook(ref Random rng)
        {
            // Exclude None, select from valid outlooks
            var validOutlooks = new NativeArray<OutlookType>(9, Allocator.Temp);
            validOutlooks[0] = OutlookType.Warlike;
            validOutlooks[1] = OutlookType.Peaceful;
            validOutlooks[2] = OutlookType.Spiritual;
            validOutlooks[3] = OutlookType.Materialistic;
            validOutlooks[4] = OutlookType.Scholarly;
            validOutlooks[5] = OutlookType.Pragmatic;
            validOutlooks[6] = OutlookType.Xenophobic;
            validOutlooks[7] = OutlookType.Egalitarian;
            validOutlooks[8] = OutlookType.Authoritarian;

            var index = rng.NextInt(0, validOutlooks.Length);
            var result = validOutlooks[index];
            validOutlooks.Dispose();
            return result;
        }
    }
}

