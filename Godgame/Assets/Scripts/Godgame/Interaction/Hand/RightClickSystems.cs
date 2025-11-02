using Godgame.Interaction;
using Godgame.Registry;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Godgame.Interaction.HandSystems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RightClickRouterSystem))]
    public partial struct RightClickProbeSystem : ISystem
    {
        private EntityQuery _handQuery;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryLookup;
        private ComponentLookup<GodgameStorehouse> _storehouseMirrorLookup;
        private ComponentLookup<ResourceChunkState> _resourceChunkLookup;
        private ComponentLookup<Parent> _parentLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputState>();
            _handQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<Hand>(),
                    ComponentType.ReadWrite<RightClickContextElement>()
                }
            });
            state.RequireForUpdate(_handQuery);

            _storehouseInventoryLookup = state.GetComponentLookup<StorehouseInventory>(isReadOnly: true);
            _storehouseMirrorLookup = state.GetComponentLookup<GodgameStorehouse>(isReadOnly: true);
            _resourceChunkLookup = state.GetComponentLookup<ResourceChunkState>(isReadOnly: true);
            _parentLookup = state.GetComponentLookup<Parent>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _storehouseInventoryLookup.Update(ref state);
            _storehouseMirrorLookup.Update(ref state);
            _resourceChunkLookup.Update(ref state);
            _parentLookup.Update(ref state);

            var elapsedSeconds = (float)state.WorldUnmanaged.Time.ElapsedTime;
            var input = SystemAPI.GetSingleton<InputState>();
            var handEntity = _handQuery.GetSingletonEntity();
            var hand = SystemAPI.GetComponentRO<Hand>(handEntity).ValueRO;
            var contexts = SystemAPI.GetBuffer<RightClickContextElement>(handEntity);

            contexts.Clear();

            // Derive a basic hit position using the cached hand world position.
            var hitPosition = hand.WorldPos;
            var hitNormal = math.up();
            var hitEntity = Entity.Null;

            // Optional physics ray for future expansion.
            if (SystemAPI.TryGetSingleton(out PhysicsWorldSingleton physicsWorld))
            {
                var rayStart = hand.WorldPos + new float3(0f, 5f, 0f);
                var rayEnd = rayStart - new float3(0f, 10f, 0f);
                var rayInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    hitPosition = hit.Position;
                    hitNormal = hit.SurfaceNormal;
                    hitEntity = hit.Entity != Entity.Null
                        ? hit.Entity
                        : (hit.RigidBodyIndex >= 0 && hit.RigidBodyIndex < physicsWorld.PhysicsWorld.Bodies.Length
                            ? physicsWorld.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity
                            : Entity.Null);
                }
            }

            var cooldownReady = elapsedSeconds >= hand.CooldownUntilSeconds;
            var hasCargo = hand.HasHeldType && hand.HeldAmount > 0;
            var hasCapacity = hand.HeldAmount < hand.HeldCapacity;
            var hasGrabbed = hand.Grabbed != Entity.Null;

            BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog = default;
            if (SystemAPI.TryGetSingleton(out ResourceTypeIndex resourceTypeIndex) && resourceTypeIndex.Catalog.IsCreated)
            {
                resourceCatalog = resourceTypeIndex.Catalog;
            }

            var storehouse = hitEntity != Entity.Null
                ? ResolveStorehouse(hitEntity, _parentLookup, _storehouseInventoryLookup, _storehouseMirrorLookup)
                : default;

            var pile = hitEntity != Entity.Null
                ? ResolvePile(hitEntity, _parentLookup, _resourceChunkLookup)
                : default;

            var pileType = pile.Found ? ResolveResourceType(pile.ResourceTypeIndex, resourceCatalog) : ResourceType.None;
            bool pileMatchesResource = !hand.HasHeldType ||
                                       pileType == ResourceType.None ||
                                       pileType == hand.HeldType;
            bool pileHasUnits = pile.Found && pile.Units > 0f && pile.Entity != Entity.Null;
            bool storehouseAcceptsCargo = storehouse.Found && storehouse.Entity != Entity.Null &&
                                          hasCargo && storehouse.AvailableCapacity > 0.01f;

            if (cooldownReady && storehouseAcceptsCargo)
            {
                contexts.Add(new RightClickContextElement
                {
                    Handler = HandRightClickHandler.StorehouseDump,
                    Priority = HandRightClickPriority.StorehouseDump,
                    Target = storehouse.Entity,
                    HitPosition = hitPosition,
                    HitNormal = hitNormal
                });
            }

            if (cooldownReady && hasCapacity && pileHasUnits && pileMatchesResource)
            {
                contexts.Add(new RightClickContextElement
                {
                    Handler = HandRightClickHandler.PileSiphon,
                    Priority = HandRightClickPriority.PileSiphon,
                    Target = pile.Entity,
                    HitPosition = hitPosition,
                    HitNormal = hitNormal
                });
            }

            if (cooldownReady && hasCargo && IsGround(storehouse, pile, hitNormal))
            {
                contexts.Add(new RightClickContextElement
                {
                    Handler = HandRightClickHandler.GroundDrip,
                    Priority = HandRightClickPriority.GroundDrip,
                    Target = Entity.Null,
                    HitPosition = hitPosition,
                    HitNormal = hitNormal
                });
            }

            if (hasGrabbed)
            {
                contexts.Add(new RightClickContextElement
                {
                    Handler = HandRightClickHandler.Drag,
                    Priority = HandRightClickPriority.Drag,
                    Target = hand.Grabbed,
                    HitPosition = hitPosition,
                    HitNormal = hitNormal
                });
            }

            if (cooldownReady && (hasGrabbed || (!hand.HasHeldType && hand.HeldAmount == 0)))
            {
                contexts.Add(new RightClickContextElement
                {
                    Handler = HandRightClickHandler.SlingshotAim,
                    Priority = HandRightClickPriority.SlingshotAim,
                    Target = hand.Grabbed,
                    HitPosition = hitPosition,
                    HitNormal = hitNormal
                });
            }
        }

        private static bool IsGround(in StorehouseResolution storehouse, in PileResolution pile, float3 hitNormal)
        {
            if (math.abs(hitNormal.y) < 0.35f)
            {
                return false;
            }

            if (pile.Found)
            {
                return false;
            }

            if (storehouse.Found && storehouse.AvailableCapacity > 0f)
            {
                return false;
            }

            return true;
        }

        private static StorehouseResolution ResolveStorehouse(
            Entity hitEntity,
            ComponentLookup<Parent> parentLookup,
            ComponentLookup<StorehouseInventory> storehouseInventoryLookup,
            ComponentLookup<GodgameStorehouse> storehouseMirrorLookup)
        {
            var result = new StorehouseResolution
            {
                Entity = Entity.Null,
                AvailableCapacity = 0f,
                Found = false
            };

            var current = hitEntity;
            var depth = 0;

            while (current != Entity.Null && depth++ < 16)
            {
                if (storehouseInventoryLookup.HasComponent(current))
                {
                    var inventory = storehouseInventoryLookup[current];
                    result.Entity = current;
                    result.AvailableCapacity = math.max(0f, inventory.TotalCapacity - inventory.TotalStored);
                    result.Found = true;
                    return result;
                }

                if (storehouseMirrorLookup.HasComponent(current))
                {
                    var mirror = storehouseMirrorLookup[current];
                    result.Entity = current;
                    result.AvailableCapacity = math.max(0f, mirror.TotalCapacity - mirror.TotalStored);
                    result.Found = true;
                    return result;
                }

                if (!parentLookup.HasComponent(current))
                {
                    break;
                }

                var parent = parentLookup[current].Value;
                if (parent == current)
                {
                    break;
                }

                current = parent;
            }

            return result;
        }

        private static PileResolution ResolvePile(
            Entity hitEntity,
            ComponentLookup<Parent> parentLookup,
            ComponentLookup<ResourceChunkState> resourceChunkLookup)
        {
            var result = new PileResolution
            {
                Entity = Entity.Null,
                Units = 0f,
                ResourceTypeIndex = 0,
                Found = false
            };

            var current = hitEntity;
            var depth = 0;

            while (current != Entity.Null && depth++ < 16)
            {
                if (resourceChunkLookup.HasComponent(current))
                {
                    var chunk = resourceChunkLookup[current];
                    result.Entity = current;
                    result.Units = chunk.Units;
                    result.ResourceTypeIndex = chunk.ResourceTypeIndex;
                    result.Found = true;
                    return result;
                }

                if (!parentLookup.HasComponent(current))
                {
                    break;
                }

                var parent = parentLookup[current].Value;
                if (parent == current)
                {
                    break;
                }

                current = parent;
            }

            return result;
        }

        private static ResourceType ResolveResourceType(ushort resourceTypeIndex, BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!catalog.IsCreated)
            {
                return ResourceType.None;
            }

            ref var blob = ref catalog.Value;
            if (resourceTypeIndex >= blob.Ids.Length)
            {
                return ResourceType.None;
            }

            var id = blob.Ids[resourceTypeIndex];

            if (Matches4(id, (byte)'w', (byte)'o', (byte)'o', (byte)'d'))
            {
                return ResourceType.Wood;
            }

            if (Matches3(id, (byte)'o', (byte)'r', (byte)'e'))
            {
                return ResourceType.Ore;
            }

            if (Matches4(id, (byte)'f', (byte)'o', (byte)'o', (byte)'d'))
            {
                return ResourceType.Food;
            }

            if (Matches7(id, (byte)'w', (byte)'o', (byte)'r', (byte)'s', (byte)'h', (byte)'i', (byte)'p'))
            {
                return ResourceType.Worship;
            }

            return ResourceType.None;
        }

        private static bool Matches3(in FixedString64Bytes value, byte c0, byte c1, byte c2)
        {
            return value.Length == 3 &&
                   value[0] == c0 &&
                   value[1] == c1 &&
                   value[2] == c2;
        }

        private static bool Matches4(in FixedString64Bytes value, byte c0, byte c1, byte c2, byte c3)
        {
            return value.Length == 4 &&
                   value[0] == c0 &&
                   value[1] == c1 &&
                   value[2] == c2 &&
                   value[3] == c3;
        }

        private static bool Matches7(in FixedString64Bytes value, byte c0, byte c1, byte c2, byte c3, byte c4, byte c5, byte c6)
        {
            return value.Length == 7 &&
                   value[0] == c0 &&
                   value[1] == c1 &&
                   value[2] == c2 &&
                   value[3] == c3 &&
                   value[4] == c4 &&
                   value[5] == c5 &&
                   value[6] == c6;
        }

        private struct StorehouseResolution
        {
            public Entity Entity;
            public float AvailableCapacity;
            public bool Found;
        }

        private struct PileResolution
        {
            public Entity Entity;
            public float Units;
            public ushort ResourceTypeIndex;
            public bool Found;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DivineHandStateSystem))]
    public partial struct RightClickRouterSystem : ISystem
    {
        private EntityQuery _handQuery;

        public void OnCreate(ref SystemState state)
        {
            _handQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<Hand>(),
                    ComponentType.ReadWrite<RightClickContextElement>(),
                    ComponentType.ReadWrite<RightClickResolved>()
                }
            });
            state.RequireForUpdate(_handQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var handEntity = _handQuery.GetSingletonEntity();
            var contexts = SystemAPI.GetBuffer<RightClickContextElement>(handEntity);
            var resolved = SystemAPI.GetComponentRW<RightClickResolved>(handEntity);

            if (contexts.Length == 0)
            {
                resolved.ValueRW = RightClickResolved.None;
                return;
            }

            var best = contexts[0];

            for (var i = 1; i < contexts.Length; i++)
            {
                var candidate = contexts[i];
                if (candidate.Priority < best.Priority)
                {
                    best = candidate;
                }
            }

            resolved.ValueRW = new RightClickResolved
            {
                HasHandler = true,
                Handler = best.Handler,
                Target = best.Target,
                HitPosition = best.HitPosition,
                HitNormal = best.HitNormal
            };
        }
    }
}
