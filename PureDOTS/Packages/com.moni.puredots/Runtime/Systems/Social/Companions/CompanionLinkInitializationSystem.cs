using PureDOTS.Runtime.Social;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Social.Companions
{
    /// <summary>
    /// Follow-up system that adds CompanionLink elements to newly created CompanionLink buffers.
    /// Runs after CompanionFormationSystem to ensure buffers exist before adding elements.
    /// Processes bonds that were just created and adds links to entities that got new buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CompanionFormationSystem))]
    public partial struct CompanionLinkInitializationSystem : ISystem
    {
        BufferLookup<CompanionLink> _companionLinkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _companionLinkLookup = state.GetBufferLookup<CompanionLink>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _companionLinkLookup.Update(ref state);

            // Process all bonds and ensure links exist
            var job = new InitializeLinksJob
            {
                CompanionLinkLookup = _companionLinkLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct InitializeLinksJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public BufferLookup<CompanionLink> CompanionLinkLookup;

            void Execute(Entity bondEntity, in CompanionBond bond)
            {
                // Ensure both entities have links to this bond
                EnsureLink(bond.A, bondEntity);
                EnsureLink(bond.B, bondEntity);
            }

            void EnsureLink(Entity entity, Entity bondEntity)
            {
                if (entity == Entity.Null)
                    return;

                if (!CompanionLinkLookup.HasBuffer(entity))
                    return;

                var links = CompanionLinkLookup[entity];
                
                // Check if link already exists
                bool hasLink = false;
                for (int i = 0; i < links.Length; i++)
                {
                    if (links[i].Bond == bondEntity)
                    {
                        hasLink = true;
                        break;
                    }
                }

                // Add link if it doesn't exist
                if (!hasLink)
                {
                    links.Add(new CompanionLink { Bond = bondEntity });
                }
            }
        }
    }
}

