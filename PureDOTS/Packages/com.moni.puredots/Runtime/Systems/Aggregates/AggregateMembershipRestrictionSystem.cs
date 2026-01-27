using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Removes villager memberships that violate aggregate membership restrictions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AggregateMembershipRestrictionSystem : ISystem
    {
        private ComponentLookup<AggregateEntity> _aggregateLookup;
        private BufferLookup<AggregateMembershipRestriction> _restrictionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _aggregateLookup = state.GetComponentLookup<AggregateEntity>(true);
            _restrictionLookup = state.GetBufferLookup<AggregateMembershipRestriction>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _aggregateLookup.Update(ref state);
            _restrictionLookup.Update(ref state);

            foreach (var memberships in SystemAPI.Query<DynamicBuffer<VillagerAggregateMembership>>())
            {
                using var categoriesPresent = new NativeBitArray(256, Unity.Collections.Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < memberships.Length; i++)
                {
                    categoriesPresent.Set((int)memberships[i].Category, true);
                }

                for (int i = memberships.Length - 1; i >= 0; i--)
                {
                    var entry = memberships[i];
                    if (!_aggregateLookup.HasComponent(entry.Aggregate))
                    {
                        memberships.RemoveAt(i);
                        continue;
                    }

                    if (!_restrictionLookup.HasBuffer(entry.Aggregate))
                    {
                        continue;
                    }

                    var restrictions = _restrictionLookup[entry.Aggregate];
                    var violated = false;
                    for (int r = 0; r < restrictions.Length; r++)
                    {
                        var disallowed = restrictions[r].DisallowedCategory;
                        if (categoriesPresent.IsSet((int)disallowed))
                        {
                            violated = true;
                            break;
                        }
                    }

                    if (violated)
                    {
                        memberships.RemoveAt(i);
                    }
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
