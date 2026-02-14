
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4X.Headless;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.BattleSlice
{
    public enum Space4XBattleSliceSteeringIntent : byte { Kite, Flank, Orbit, Evade }
    public enum Space4XBattleSliceWeaponKind : byte { RaycastGun, SweptProjectile, FlakBurst }

    public struct Space4XBattleSlice01Tag : IComponentData { }

    public struct Space4XBattleSliceFighter : IComponentData
    {
        public byte Side;
        public byte Alive;
        public Space4XBattleSliceSteeringIntent Intent;
        public Space4XBattleSliceWeaponKind Weapon;
        public float Speed;
        public float Range;
        public float Damage;
        public float ProjectileSpeed;
        public float3 Velocity;
        public uint FireInterval;
        public uint LastFireTick;
    }

    public struct Space4XBattleSliceProjectile : IComponentData
    {
        public byte Side;
        public float Damage;
        public float Radius;
        public float3 Velocity;
        public uint ExpireTick;
    }

    public struct Space4XBattleSliceFlakVolume : IComponentData
    {
        public byte Side;
        public float Radius;
        public float DamagePerTick;
        public uint ExpireTick;
    }

    public struct Space4XBattleSliceMetrics : IComponentData
    {
        public int FightersTotal;
        public int FightersAlive;
        public int FightersDestroyed;
        public int Side0Alive;
        public int Side1Alive;
        public int RaycastShots;
        public int RaycastHits;
        public int ProjectileShots;
        public int ProjectileHits;
        public int FlakBursts;
        public int FlakHits;
        public int DamageEvents;
        public uint Digest;
        public uint LastSnapshotTick;
        public byte Emitted;
    }

    internal static class Space4XBattleSlice01
    {
        public static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_battle_slice_01");

        public static uint Mix(uint digest, uint a, uint b, uint c)
            => math.hash(new uint4(digest ^ 0x9E3779B9u, a + 0x85EBCA6Bu, b + 0xC2B2AE35u, c + 0x27D4EB2Fu));

        public static float Roll(Entity entity, uint tick, uint salt)
            => (math.hash(new uint4((uint)entity.Index, (uint)entity.Version, tick, salt)) & 0xFFFFu) / 65535f;

        public static float SegmentDistanceSq(float3 point, float3 a, float3 b)
        {
            var ab = b - a;
            var lenSq = math.lengthsq(ab);
            if (lenSq <= 1e-6f)
            {
                return math.lengthsq(point - a);
            }

            var t = math.saturate(math.dot(point - a, ab) / lenSq);
            return math.lengthsq(point - (a + ab * t));
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XBattleSliceBootstrapSystem : ISystem
    {
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0 ||
                !SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XBattleSlice01.ScenarioId))
            {
                return;
            }

            var side0Pos = float3.zero;
            var side1Pos = float3.zero;
            var has0 = false;
            var has1 = false;

            foreach (var (side, tx) in SystemAPI.Query<RefRO<Space4X.Runtime.ScenarioSide>, RefRO<LocalTransform>>())
            {
                if (!has0 && side.ValueRO.Side == 0)
                {
                    side0Pos = tx.ValueRO.Position;
                    has0 = true;
                }
                else if (!has1 && side.ValueRO.Side == 1)
                {
                    side1Pos = tx.ValueRO.Position;
                    has1 = true;
                }
            }

            if (!has0 || !has1)
            {
                return;
            }

            SpawnFighters(ref state, side0Pos, 0, 80);
            SpawnFighters(ref state, side1Pos, 1, 80);
            SpawnFlak(state.EntityManager, side0Pos + new float3(220f, 0f, 40f), 0, 24f, 0.22f, 0u);
            SpawnFlak(state.EntityManager, side1Pos + new float3(-220f, 0f, -40f), 1, 24f, 0.22f, 0u);

            var metrics = state.EntityManager.CreateEntity(typeof(Space4XBattleSlice01Tag), typeof(Space4XBattleSliceMetrics));
            state.EntityManager.SetComponentData(metrics, new Space4XBattleSliceMetrics { FightersTotal = 160, Digest = 1u });

            _done = 1;
            UnityEngine.Debug.Log("[BattleSlice01] spawned fighters=160 flak_anchors=2");
        }

        private static void SpawnFighters(ref SystemState state, float3 anchor, byte side, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = math.radians(i * (360f / math.max(1, count)));
                var radius = 45f + (i % 4) * 8f;
                var pos = anchor + new float3(math.cos(angle) * radius * (side == 0 ? 1f : -1f), -4f + (i % 6), math.sin(angle) * radius);
                var weapon = (Space4XBattleSliceWeaponKind)((i + side) % 3);
                var entity = state.EntityManager.CreateEntity(typeof(Space4XBattleSlice01Tag), typeof(LocalTransform), typeof(Space4XBattleSliceFighter));

                state.EntityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 0.6f));
                state.EntityManager.SetComponentData(entity, new Space4XBattleSliceFighter
                {
                    Side = side,
                    Alive = 1,
                    Intent = (Space4XBattleSliceSteeringIntent)((i + side) % 4),
                    Weapon = weapon,
                    Speed = weapon == Space4XBattleSliceWeaponKind.SweptProjectile ? 22f : 24f,
                    Range = weapon == Space4XBattleSliceWeaponKind.RaycastGun ? 140f : 180f,
                    Damage = weapon == Space4XBattleSliceWeaponKind.RaycastGun ? 6f : (weapon == Space4XBattleSliceWeaponKind.SweptProjectile ? 11f : 3f),
                    ProjectileSpeed = weapon == Space4XBattleSliceWeaponKind.SweptProjectile ? 75f : 0f,
                    Velocity = side == 0 ? new float3(10f, 0f, 0f) : new float3(-10f, 0f, 0f),
                    FireInterval = weapon == Space4XBattleSliceWeaponKind.RaycastGun ? 6u : (weapon == Space4XBattleSliceWeaponKind.SweptProjectile ? 14u : 18u),
                    LastFireTick = 0u
                });
            }
        }

        private static void SpawnFlak(EntityManager em, float3 position, byte side, float radius, float dps, uint expireTick)
        {
            var entity = em.CreateEntity(typeof(Space4XBattleSlice01Tag), typeof(LocalTransform), typeof(Space4XBattleSliceFlakVolume));
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new Space4XBattleSliceFlakVolume { Side = side, Radius = radius, DamagePerTick = dps, ExpireTick = expireTick });
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBattleSliceBootstrapSystem))]
    public partial struct Space4XBattleSliceRuntimeSystem : ISystem
    {
        private struct FighterSnapshot
        {
            public Entity Entity;
            public byte Side;
            public float3 Position;
            public float3 Velocity;
        }

        private struct FlakSnapshot
        {
            public byte Side;
            public float3 Position;
            public float Radius;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XBattleSliceMetrics>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) ||
                !info.ScenarioId.Equals(Space4XBattleSlice01.ScenarioId) ||
                !SystemAPI.TryGetSingletonEntity<Space4XBattleSliceMetrics>(out var metricsEntity))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var dt = math.max(1e-4f, SystemAPI.Time.DeltaTime);
            var em = state.EntityManager;
            var metrics = em.GetComponentData<Space4XBattleSliceMetrics>(metricsEntity);
            var fighters = new NativeList<FighterSnapshot>(Allocator.Temp);
            var flakHazards = new NativeList<FlakSnapshot>(Allocator.Temp);

            foreach (var (fighter, tx, entity) in SystemAPI.Query<RefRO<Space4XBattleSliceFighter>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (fighter.ValueRO.Alive == 0)
                {
                    continue;
                }

                fighters.Add(new FighterSnapshot
                {
                    Entity = entity,
                    Side = fighter.ValueRO.Side,
                    Position = tx.ValueRO.Position,
                    Velocity = fighter.ValueRO.Velocity
                });
            }

            if (fighters.Length == 0)
            {
                fighters.Dispose();
                flakHazards.Dispose();
                return;
            }

            foreach (var (volume, tx) in SystemAPI.Query<RefRO<Space4XBattleSliceFlakVolume>, RefRO<LocalTransform>>())
            {
                flakHazards.Add(new FlakSnapshot
                {
                    Side = volume.ValueRO.Side,
                    Position = tx.ValueRO.Position,
                    Radius = volume.ValueRO.Radius
                });
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (fighter, tx, entity) in SystemAPI.Query<RefRW<Space4XBattleSliceFighter>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                if (fighter.ValueRO.Alive == 0)
                {
                    continue;
                }

                if (!TryFindNearestEnemy(fighters, fighter.ValueRO.Side, entity, tx.ValueRO.Position, out var target))
                {
                    continue;
                }

                var toEnemy = math.normalizesafe(target.Position - tx.ValueRO.Position, new float3(1f, 0f, 0f));
                var right = math.normalizesafe(math.cross(math.up(), toEnemy), new float3(0f, 0f, 1f));
                var steer = ResolveSteer(entity, fighter.ValueRO.Intent, toEnemy, right, time.Tick);
                for (var i = 0; i < flakHazards.Length; i++)
                {
                    var hazard = flakHazards[i];
                    if (hazard.Side == fighter.ValueRO.Side)
                    {
                        continue;
                    }

                    var delta = tx.ValueRO.Position - hazard.Position;
                    var dist = math.length(delta);
                    var avoidRadius = hazard.Radius + 20f;
                    if (dist <= 0.01f || dist >= avoidRadius)
                    {
                        continue;
                    }

                    steer += math.normalizesafe(delta) * (1f - dist / avoidRadius) * 1.6f;
                }
                steer = math.normalizesafe(steer, toEnemy);

                fighter.ValueRW.Velocity = math.lerp(fighter.ValueRO.Velocity, steer * fighter.ValueRO.Speed, 0.22f);
                var nextPosition = tx.ValueRO.Position + fighter.ValueRO.Velocity * dt;
                tx.ValueRW = LocalTransform.FromPositionRotationScale(nextPosition, quaternion.LookRotationSafe(math.normalizesafe(fighter.ValueRO.Velocity, steer), math.up()), tx.ValueRO.Scale);

                var distance = math.distance(tx.ValueRO.Position, target.Position);
                if (distance > fighter.ValueRO.Range)
                {
                    continue;
                }

                if (time.Tick > fighter.ValueRO.LastFireTick && time.Tick - fighter.ValueRO.LastFireTick < fighter.ValueRO.FireInterval)
                {
                    continue;
                }

                fighter.ValueRW.LastFireTick = time.Tick;

                if (fighter.ValueRO.Weapon == Space4XBattleSliceWeaponKind.RaycastGun)
                {
                    metrics.RaycastShots++;
                    metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)entity.Index, (uint)target.Entity.Index, 1u);
                    if (Space4XBattleSlice01.Roll(entity, time.Tick, 11u) <= 0.72f && ApplyDamage(em, target.Entity, fighter.ValueRO.Damage, ref metrics))
                    {
                        metrics.RaycastHits++;
                    }
                }
                else if (fighter.ValueRO.Weapon == Space4XBattleSliceWeaponKind.SweptProjectile)
                {
                    metrics.ProjectileShots++;
                    metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)entity.Index, (uint)target.Entity.Index, 2u);
                    var projectile = ecb.CreateEntity();
                    ecb.AddComponent(projectile, new Space4XBattleSlice01Tag());
                    ecb.AddComponent(projectile, LocalTransform.FromPositionRotationScale(tx.ValueRO.Position, quaternion.identity, 0.2f));
                    ecb.AddComponent(projectile, new Space4XBattleSliceProjectile
                    {
                        Side = fighter.ValueRO.Side,
                        Damage = fighter.ValueRO.Damage,
                        Radius = 2.2f,
                        Velocity = math.normalizesafe(target.Position - tx.ValueRO.Position) * math.max(1f, fighter.ValueRO.ProjectileSpeed),
                        ExpireTick = time.Tick + 72u
                    });
                }
                else
                {
                    metrics.FlakBursts++;
                    metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)entity.Index, (uint)target.Entity.Index, 3u);

                    var burstCenter = target.Position + target.Velocity * 0.2f;
                    const float burstRadius = 12f;
                    var burstRadiusSq = burstRadius * burstRadius;
                    var burstHits = 0;
                    for (var i = 0; i < fighters.Length; i++)
                    {
                        var candidate = fighters[i];
                        if (candidate.Side == fighter.ValueRO.Side)
                        {
                            continue;
                        }

                        if (math.lengthsq(candidate.Position - burstCenter) > burstRadiusSq)
                        {
                            continue;
                        }

                        if (ApplyDamage(em, candidate.Entity, fighter.ValueRO.Damage * 0.45f, ref metrics))
                        {
                            metrics.FlakHits++;
                            burstHits++;
                        }
                    }

                    if (burstHits > 0)
                    {
                        metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)entity.Index, (uint)burstHits, 13u);
                    }
                }
            }

            foreach (var (projectile, tx, projectileEntity) in SystemAPI.Query<RefRO<Space4XBattleSliceProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var start = tx.ValueRO.Position;
                var end = start + projectile.ValueRO.Velocity * dt;
                tx.ValueRW = LocalTransform.FromPositionRotationScale(end, quaternion.identity, tx.ValueRO.Scale);
                var radiusSq = projectile.ValueRO.Radius * projectile.ValueRO.Radius;
                var hit = Entity.Null;
                var best = float.MaxValue;

                for (var i = 0; i < fighters.Length; i++)
                {
                    var candidate = fighters[i];
                    if (candidate.Side == projectile.ValueRO.Side)
                    {
                        continue;
                    }

                    var d2 = Space4XBattleSlice01.SegmentDistanceSq(candidate.Position, start, end);
                    if (d2 <= radiusSq && d2 < best)
                    {
                        hit = candidate.Entity;
                        best = d2;
                    }
                }

                if (hit != Entity.Null)
                {
                    if (ApplyDamage(em, hit, projectile.ValueRO.Damage, ref metrics))
                    {
                        metrics.ProjectileHits++;
                        metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)projectileEntity.Index, (uint)hit.Index, 5u);
                    }

                    ecb.DestroyEntity(projectileEntity);
                }
                else if (projectile.ValueRO.ExpireTick > 0u && time.Tick >= projectile.ValueRO.ExpireTick)
                {
                    ecb.DestroyEntity(projectileEntity);
                }
            }

            foreach (var (flak, tx, flakEntity) in SystemAPI.Query<RefRO<Space4XBattleSliceFlakVolume>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var radiusSq = flak.ValueRO.Radius * flak.ValueRO.Radius;
                var hits = 0;

                for (var i = 0; i < fighters.Length; i++)
                {
                    var candidate = fighters[i];
                    if (flak.ValueRO.Side != 255 && candidate.Side == flak.ValueRO.Side)
                    {
                        continue;
                    }

                    if (math.lengthsq(candidate.Position - tx.ValueRO.Position) > radiusSq)
                    {
                        continue;
                    }

                    if (ApplyDamage(em, candidate.Entity, flak.ValueRO.DamagePerTick, ref metrics))
                    {
                        metrics.FlakHits++;
                        hits++;
                    }
                }

                if (hits > 0)
                {
                    metrics.Digest = Space4XBattleSlice01.Mix(metrics.Digest, (uint)flakEntity.Index, (uint)hits, 7u);
                }

                if (flak.ValueRO.ExpireTick > 0u && time.Tick >= flak.ValueRO.ExpireTick)
                {
                    ecb.DestroyEntity(flakEntity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
            fighters.Dispose();
            flakHazards.Dispose();

            var alive = 0;
            var side0 = 0;
            var side1 = 0;
            foreach (var fighter in SystemAPI.Query<RefRO<Space4XBattleSliceFighter>>())
            {
                if (fighter.ValueRO.Alive == 0)
                {
                    continue;
                }

                alive++;
                if (fighter.ValueRO.Side == 0)
                {
                    side0++;
                }
                else if (fighter.ValueRO.Side == 1)
                {
                    side1++;
                }
            }

            metrics.FightersAlive = alive;
            metrics.FightersDestroyed = math.max(0, metrics.FightersTotal - alive);
            metrics.Side0Alive = side0;
            metrics.Side1Alive = side1;
            var fired = metrics.RaycastShots + metrics.ProjectileShots + metrics.FlakBursts;
            var hitsTotal = metrics.RaycastHits + metrics.ProjectileHits + metrics.FlakHits;
            var winner = Winner(metrics.Side0Alive, metrics.Side1Alive);
            var winnerAlive = winner == 0 ? metrics.Side0Alive : (winner == 1 ? metrics.Side1Alive : 0);
            var winnerRatio = metrics.FightersAlive > 0 ? winnerAlive / math.max(1f, metrics.FightersAlive) : 0f;

            if (time.Tick % 50u == 0u && metrics.LastSnapshotTick != time.Tick)
            {
                metrics.LastSnapshotTick = time.Tick;
                UnityEngine.Debug.Log($"[BattleSlice01Metrics] tick={time.Tick} space4x.combat.shots.fired_total={fired} space4x.combat.shots.hit_total={hitsTotal} space4x.combat.combatants.destroyed={metrics.FightersDestroyed} space4x.battle.fire.raycast_hits={metrics.RaycastHits} space4x.battle.fire.projectile_hits={metrics.ProjectileHits} space4x.battle.fire.flak_hits={metrics.FlakHits} space4x.battle.determinism.digest={metrics.Digest}");
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (metrics.Emitted == 0 && runtime.EndTick > 0u && time.Tick >= runtime.EndTick)
            {
                if (Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
                {
                    AddMetric(buffer, "space4x.intercept.attempts", fired);
                    AddMetric(buffer, "space4x.hull.damaged", metrics.DamageEvents);
                    AddMetric(buffer, "space4x.hull.critical", metrics.FightersDestroyed);
                    AddMetric(buffer, "space4x.combat.shots.fired_total", fired);
                    AddMetric(buffer, "space4x.combat.shots.hit_total", hitsTotal);
                    AddMetric(buffer, "space4x.combat.combatants.total", metrics.FightersTotal);
                    AddMetric(buffer, "space4x.combat.combatants.destroyed", metrics.FightersDestroyed);
                    AddMetric(buffer, "space4x.combat.outcome.total_alive", metrics.FightersAlive);
                    AddMetric(buffer, "space4x.combat.outcome.winner_side", winner);
                    AddMetric(buffer, "space4x.combat.outcome.winner_alive", winnerAlive);
                    AddMetric(buffer, "space4x.combat.outcome.winner_ratio", winnerRatio);
                    AddMetric(buffer, "space4x.battle.fire.raycast_hits", metrics.RaycastHits);
                    AddMetric(buffer, "space4x.battle.fire.projectile_hits", metrics.ProjectileHits);
                    AddMetric(buffer, "space4x.battle.fire.flak_hits", metrics.FlakHits);
                    AddMetric(buffer, "space4x.battle.determinism.digest", metrics.Digest);
                    metrics.Emitted = 1;
                }
            }

            em.SetComponentData(metricsEntity, metrics);
        }

        private static bool TryFindNearestEnemy(NativeList<FighterSnapshot> fighters, byte side, Entity self, float3 position, out FighterSnapshot nearest)
        {
            nearest = default;
            var found = false;
            var best = float.MaxValue;
            for (var i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f.Entity == self || f.Side == side)
                {
                    continue;
                }

                var d2 = math.lengthsq(f.Position - position);
                if (d2 < best)
                {
                    best = d2;
                    nearest = f;
                    found = true;
                }
            }

            return found;
        }

        private static float3 ResolveSteer(Entity entity, Space4XBattleSliceSteeringIntent intent, float3 toEnemy, float3 right, uint tick)
        {
            switch (intent)
            {
                case Space4XBattleSliceSteeringIntent.Kite: return math.normalizesafe(-toEnemy + right * 0.35f, -toEnemy);
                case Space4XBattleSliceSteeringIntent.Flank: return math.normalizesafe(toEnemy + right * 0.65f, toEnemy);
                case Space4XBattleSliceSteeringIntent.Orbit: return math.normalizesafe(right * (((entity.Index & 1) == 0) ? 1f : -1f) + toEnemy * 0.15f, right);
                case Space4XBattleSliceSteeringIntent.Evade:
                {
                    var jitter = new float3(Space4XBattleSlice01.Roll(entity, tick, 17u) * 2f - 1f, 0f, Space4XBattleSlice01.Roll(entity, tick, 23u) * 2f - 1f);
                    return math.normalizesafe(-toEnemy + jitter * 0.85f, -toEnemy);
                }
                default: return toEnemy;
            }
        }

        private static bool ApplyDamage(EntityManager em, Entity target, float damage, ref Space4XBattleSliceMetrics metrics)
        {
            if (target == Entity.Null || !em.HasComponent<Space4XBattleSliceFighter>(target))
            {
                return false;
            }

            var fighter = em.GetComponentData<Space4XBattleSliceFighter>(target);
            if (fighter.Alive == 0)
            {
                return false;
            }

            var current = fighter.Speed;
            current = math.max(0f, current - math.max(0f, damage) * 0.08f);
            fighter.Speed = math.max(current, 6f);
            metrics.DamageEvents++;

            if (current <= 1f)
            {
                fighter.Alive = 0;
            }

            em.SetComponentData(target, fighter);
            return true;
        }

        private static int Winner(int side0, int side1)
        {
            if (side0 > side1) return 0;
            if (side1 > side0) return 1;
            return -1;
        }

        private static void AddMetric(DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
        {
            var fixedKey = new FixedString64Bytes(key);
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (metric.Key.Equals(fixedKey))
                {
                    metric.Value = value;
                    buffer[i] = metric;
                    return;
                }
            }

            buffer.Add(new Space4XOperatorMetric { Key = fixedKey, Value = value });
        }
    }
}
