using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Headless;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Modules.PipelineV0
{
    public enum Space4XModuleDivisionId : byte { Cooling, Power, Optics, Mount, Firmware }

    public struct Space4XModulePipelineMicroTag : IComponentData { }

    public struct FacilityDivisionCapability : IComponentData
    {
        public Space4XModuleDivisionId DivisionId;
        public float MinSpec;
        public float Maturity;
    }

    public struct WorkforceQuality : IComponentData
    {
        public float Skill;
        public byte Seats;
    }

    [InternalBufferCapacity(8)]
    public struct DivisionSeat : IBufferElementData
    {
        public byte SeatIndex;
        public float Skill;
    }

    public struct MaterialBatch : IComponentData
    {
        public float Units;
        public float Quality;
    }

    public struct OrgCompanyPlan : IComponentData
    {
        public uint DesiredBlueprintId;
        public int DesiredBlueprintQueue;
        public float QualityTarget;
        public float StrategyQualityBias;
        public int RejectStreak;
        public int BacklogPressure;
        public uint RerouteUntilTick;
    }

    public struct BlueprintProvenance : IComponentData
    {
        public uint BlueprintId;
        public float ProcessMaturity;
    }

    public struct ModulePipelineShipyard : IComponentData
    {
        public Entity Ship;
        public byte NextSlot;
        public float AssemblerMaturity;
        public float InstallMaturity;
    }

    public struct ModulePipelineMetrics : IComponentData
    {
        public int LimbsProduced;
        public int Assembled;
        public int Installed;
        public int Scrap;
        public int Cc;
        public int Pc;
        public int Oc;
        public int Mc;
        public int Fc;
        public float Cq;
        public float Pq;
        public float Oq;
        public float Mq;
        public float Fq;
        public float IntegrationSum;
        public float InstallSum;
        public uint Digest;
        public uint LastSnap;
        public byte Emitted;
    }

    [InternalBufferCapacity(128)]
    public struct LimbBatch : IBufferElementData
    {
        public uint BlueprintId;
        public Space4XModuleDivisionId DivisionId;
        public float Quality;
    }

    [InternalBufferCapacity(16)]
    public struct ModuleItem : IBufferElementData
    {
        public uint BlueprintId;
        public float IntegrationQuality;
    }

    [InternalBufferCapacity(16)]
    public struct ShipInstalledModule : IBufferElementData
    {
        public byte Slot;
        public float InstallQuality;
        public float EffectiveHeat;
        public float EffectiveRof;
        public float EffectiveReliability;
    }

    public struct ModuleShipStats : IComponentData
    {
        public int Installed;
        public float Heat;
        public float Rof;
        public float Reliability;
    }

    internal static class PipelineConst
    {
        public static readonly FixedString64Bytes ScenarioId = new("space4x_module_pipeline_micro");
        public static uint Mix(uint d, uint a, uint b, uint c) => math.hash(new uint4(d ^ 0x9E3779B9u, a + 0x85EBCA6Bu, b + 0xC2B2AE35u, c + 0x27D4EB2Fu));
        public static float Avg(float sum, int count) => count > 0 ? sum / math.max(1, count) : 0f;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XModulePipelineBootstrapSystem : ISystem
    {
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0 || !SystemAPI.TryGetSingleton(out ScenarioInfo info) || !info.ScenarioId.Equals(PipelineConst.ScenarioId))
            {
                return;
            }

            var metricsEntity = state.EntityManager.CreateEntity(typeof(Space4XModulePipelineMicroTag), typeof(ModulePipelineMetrics));
            state.EntityManager.SetComponentData(metricsEntity, new ModulePipelineMetrics { Digest = 1u });
            state.EntityManager.AddBuffer<LimbBatch>(metricsEntity);

            var org = state.EntityManager.CreateEntity(typeof(Space4XModulePipelineMicroTag), typeof(OrgCompanyPlan));
            state.EntityManager.SetComponentData(org, new OrgCompanyPlan
            {
                DesiredBlueprintId = 1001u,
                DesiredBlueprintQueue = 3,
                QualityTarget = 0.72f,
                StrategyQualityBias = 0.62f
            });

            var ship = state.EntityManager.CreateEntity(typeof(Space4XModulePipelineMicroTag), typeof(ModuleShipStats));
            state.EntityManager.SetComponentData(ship, new ModuleShipStats { Heat = 1f, Rof = 1f, Reliability = 1f });
            state.EntityManager.AddBuffer<ShipInstalledModule>(ship);

            var shipyard = state.EntityManager.CreateEntity(typeof(Space4XModulePipelineMicroTag), typeof(ModulePipelineShipyard), typeof(BlueprintProvenance));
            state.EntityManager.SetComponentData(shipyard, new ModulePipelineShipyard { Ship = ship, AssemblerMaturity = 0.68f, InstallMaturity = 0.74f });
            state.EntityManager.SetComponentData(shipyard, new BlueprintProvenance { BlueprintId = 1001u, ProcessMaturity = 0.71f });
            state.EntityManager.AddBuffer<ModuleItem>(shipyard);

            CreateDivision(ref state, Space4XModuleDivisionId.Cooling, 0.67f, 0.73f, 0.58f);
            CreateDivision(ref state, Space4XModuleDivisionId.Power, 0.64f, 0.75f, 0.57f);
            CreateDivision(ref state, Space4XModuleDivisionId.Optics, 0.66f, 0.72f, 0.59f);
            CreateDivision(ref state, Space4XModuleDivisionId.Mount, 0.62f, 0.79f, 0.55f);
            CreateDivision(ref state, Space4XModuleDivisionId.Firmware, 0.68f, 0.76f, 0.60f);

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var dt = math.max(1e-4f, SystemAPI.GetSingleton<TimeState>().FixedDeltaTime > 0f ? SystemAPI.GetSingleton<TimeState>().FixedDeltaTime : (1f / 60f));
            var expected = runtime.StartTick + (uint)math.max(1f, math.ceil(runtime.DurationSeconds / dt));
            Debug.Log($"[ModulePipelineV0] expected_end_tick={expected} duration_s={runtime.DurationSeconds:0.###} fixed_dt={dt:0.######}");
            _done = 1;
        }

        private static void CreateDivision(ref SystemState state, Space4XModuleDivisionId id, float maturity, float skill, float materialQuality)
        {
            var e = state.EntityManager.CreateEntity(typeof(Space4XModulePipelineMicroTag), typeof(FacilityDivisionCapability), typeof(WorkforceQuality), typeof(MaterialBatch));
            state.EntityManager.SetComponentData(e, new FacilityDivisionCapability { DivisionId = id, MinSpec = 0.56f, Maturity = maturity });
            state.EntityManager.SetComponentData(e, new WorkforceQuality { Seats = 4, Skill = skill });
            state.EntityManager.SetComponentData(e, new MaterialBatch { Units = 0.86f, Quality = materialQuality });

            var seats = state.EntityManager.AddBuffer<DivisionSeat>(e);
            for (byte i = 0; i < 4; i++)
            {
                seats.Add(new DivisionSeat
                {
                    SeatIndex = i,
                    Skill = math.saturate(skill * (0.94f + i * 0.02f))
                });
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModulePipelineBootstrapSystem))]
    public partial struct Space4XModulePipelineRuntimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ModulePipelineMetrics>();
            state.RequireForUpdate<OrgCompanyPlan>();
            state.RequireForUpdate<ModulePipelineShipyard>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) || !info.ScenarioId.Equals(PipelineConst.ScenarioId) ||
                !SystemAPI.TryGetSingletonEntity<ModulePipelineMetrics>(out var metricsEntity) ||
                !SystemAPI.TryGetSingletonEntity<OrgCompanyPlan>(out var orgEntity))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var metrics = state.EntityManager.GetComponentData<ModulePipelineMetrics>(metricsEntity);
            var org = state.EntityManager.GetComponentData<OrgCompanyPlan>(orgEntity);
            var limbs = state.EntityManager.GetBuffer<LimbBatch>(metricsEntity);

            var moduleQueueEntity = Entity.Null;
            DynamicBuffer<ModuleItem> moduleQueue = default;
            ModulePipelineShipyard shipyard = default;
            BlueprintProvenance provenance = default;
            foreach (var (shipyardRw, prov, queue, e) in SystemAPI.Query<RefRW<ModulePipelineShipyard>, RefRO<BlueprintProvenance>, DynamicBuffer<ModuleItem>>().WithAll<Space4XModulePipelineMicroTag>().WithEntityAccess())
            {
                moduleQueueEntity = e;
                moduleQueue = queue;
                shipyard = shipyardRw.ValueRO;
                provenance = prov.ValueRO;
                break;
            }

            if (moduleQueueEntity == Entity.Null)
            {
                return;
            }

            // AI policy: deterministic quality target steering by backlog/rejects.
            var backlog = math.max(0, org.DesiredBlueprintQueue - metrics.Installed) + math.max(0, (limbs.Length / 5) - moduleQueue.Length);
            org.BacklogPressure = backlog;
            if (backlog >= 4)
            {
                org.QualityTarget -= 0.03f;
            }
            else
            {
                org.QualityTarget += 0.02f;
            }
            if (org.RejectStreak >= 3)
            {
                org.QualityTarget -= 0.05f;
                org.RerouteUntilTick = time.Tick + 40u;
            }
            org.QualityTarget = math.clamp(org.QualityTarget, 0.38f, 0.92f);

            // Division production: materials -> limb divisions with reject/scrap.
            foreach (var (cap, workRw, matRw, seatBuffer) in SystemAPI.Query<RefRO<FacilityDivisionCapability>, RefRW<WorkforceQuality>, RefRW<MaterialBatch>, DynamicBuffer<DivisionSeat>>().WithAll<Space4XModulePipelineMicroTag>())
            {
                var work = workRw.ValueRW;
                var mat = matRw.ValueRW;
                if (mat.Units <= 0.01f)
                {
                    continue;
                }

                var seatFactor = 0f;
                if (seatBuffer.Length > 0)
                {
                    for (var i = 0; i < seatBuffer.Length; i++)
                    {
                        seatFactor += seatBuffer[i].Skill;
                    }
                    seatFactor /= math.max(1, seatBuffer.Length);
                }
                else
                {
                    seatFactor = work.Skill;
                }
                var quality = mat.Quality * 0.45f + work.Skill * 0.30f + seatFactor * 0.15f + cap.ValueRO.Maturity * 0.10f + org.StrategyQualityBias * 0.03f - math.saturate(backlog * 0.08f) * 0.04f;
                quality = math.saturate(quality);
                var minSpec = math.max(cap.ValueRO.MinSpec, org.QualityTarget * 0.80f);
                var reject = quality < minSpec;

                mat.Units = math.max(0f, mat.Units - 0.20f);
                if (org.RerouteUntilTick > time.Tick)
                {
                    mat.Quality = math.saturate(mat.Quality + 0.002f);
                }

                if (reject)
                {
                    metrics.Scrap++;
                    org.RejectStreak++;
                    work.Skill = math.clamp(work.Skill - 0.001f, 0.35f, 0.98f);
                    metrics.Digest = PipelineConst.Mix(metrics.Digest, time.Tick, (uint)cap.ValueRO.DivisionId, 0u);
                }
                else
                {
                    if (limbs.Length >= 256)
                    {
                        limbs.RemoveAt(0);
                        metrics.Scrap++;
                    }

                    limbs.Add(new LimbBatch { BlueprintId = org.DesiredBlueprintId, DivisionId = cap.ValueRO.DivisionId, Quality = quality });
                    metrics.LimbsProduced++;
                    AddDivisionQuality(ref metrics, cap.ValueRO.DivisionId, quality);
                    org.RejectStreak = math.max(0, org.RejectStreak - 1);
                    work.Skill = math.clamp(work.Skill + 0.0006f, 0.35f, 0.98f);
                    metrics.Digest = PipelineConst.Mix(metrics.Digest, time.Tick, (uint)cap.ValueRO.DivisionId, (uint)math.round(quality * 1000f));
                }

                workRw.ValueRW = work;
                matRw.ValueRW = mat;
            }

            // Module assembly: consume one set of limb batches per tick.
            if (moduleQueue.Length < 32 &&
                TryTake(ref limbs, org.DesiredBlueprintId, Space4XModuleDivisionId.Cooling, out var q0) &&
                TryTake(ref limbs, org.DesiredBlueprintId, Space4XModuleDivisionId.Power, out var q1) &&
                TryTake(ref limbs, org.DesiredBlueprintId, Space4XModuleDivisionId.Optics, out var q2) &&
                TryTake(ref limbs, org.DesiredBlueprintId, Space4XModuleDivisionId.Mount, out var q3) &&
                TryTake(ref limbs, org.DesiredBlueprintId, Space4XModuleDivisionId.Firmware, out var q4))
            {
                var avg = (q0 + q1 + q2 + q3 + q4) / 5f;
                var maturity = math.saturate((provenance.ProcessMaturity + shipyard.AssemblerMaturity) * 0.5f);
                var integrationQuality = math.saturate(avg * math.lerp(0.86f, 1.18f, maturity));
                moduleQueue.Add(new ModuleItem { BlueprintId = org.DesiredBlueprintId, IntegrationQuality = integrationQuality });
                metrics.Assembled++;
                metrics.IntegrationSum += integrationQuality;
                metrics.Digest = PipelineConst.Mix(metrics.Digest, time.Tick, (uint)metrics.Assembled, (uint)math.round(integrationQuality * 1000f));
            }

            // Shipyard integration: install one module every 6 ticks.
            if (time.Tick % 6u == 0u && moduleQueue.Length > 0 && state.EntityManager.HasComponent<ModuleShipStats>(shipyard.Ship) && state.EntityManager.HasBuffer<ShipInstalledModule>(shipyard.Ship))
            {
                var item = moduleQueue[0];
                moduleQueue.RemoveAt(0);
                var installQuality = math.saturate(item.IntegrationQuality * math.lerp(0.80f, 1.14f, shipyard.InstallMaturity));
                var heat = 1f + (1f - installQuality) * 0.22f;
                var rof = math.max(0.6f, installQuality);
                var reliability = math.saturate(0.55f + installQuality * 0.45f);

                var shipStats = state.EntityManager.GetComponentData<ModuleShipStats>(shipyard.Ship);
                shipStats.Installed++;
                var oldCount = math.max(0, shipStats.Installed - 1);
                shipStats.Heat = ((shipStats.Heat * oldCount) + heat) / math.max(1, shipStats.Installed);
                shipStats.Rof = ((shipStats.Rof * oldCount) + rof) / math.max(1, shipStats.Installed);
                shipStats.Reliability = ((shipStats.Reliability * oldCount) + reliability) / math.max(1, shipStats.Installed);
                state.EntityManager.SetComponentData(shipyard.Ship, shipStats);

                var installed = state.EntityManager.GetBuffer<ShipInstalledModule>(shipyard.Ship);
                installed.Add(new ShipInstalledModule
                {
                    Slot = shipyard.NextSlot,
                    InstallQuality = installQuality,
                    EffectiveHeat = heat,
                    EffectiveRof = rof,
                    EffectiveReliability = reliability
                });
                shipyard.NextSlot++;
                state.EntityManager.SetComponentData(moduleQueueEntity, shipyard);

                metrics.Installed++;
                metrics.InstallSum += installQuality;
                metrics.Digest = PipelineConst.Mix(metrics.Digest, time.Tick, (uint)metrics.Installed, (uint)math.round(installQuality * 1000f));
            }

            state.EntityManager.SetComponentData(metricsEntity, metrics);
            state.EntityManager.SetComponentData(orgEntity, org);
        }

        private static void AddDivisionQuality(ref ModulePipelineMetrics m, Space4XModuleDivisionId id, float q)
        {
            switch (id)
            {
                case Space4XModuleDivisionId.Cooling: m.Cc++; m.Cq += q; break;
                case Space4XModuleDivisionId.Power: m.Pc++; m.Pq += q; break;
                case Space4XModuleDivisionId.Optics: m.Oc++; m.Oq += q; break;
                case Space4XModuleDivisionId.Mount: m.Mc++; m.Mq += q; break;
                case Space4XModuleDivisionId.Firmware: m.Fc++; m.Fq += q; break;
            }
        }

        private static bool TryTake(ref DynamicBuffer<LimbBatch> limbs, uint blueprintId, Space4XModuleDivisionId id, out float quality)
        {
            quality = 0f;
            for (var i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (limb.BlueprintId != blueprintId || limb.DivisionId != id)
                {
                    continue;
                }

                quality = limb.Quality;
                limbs.RemoveAt(i);
                return true;
            }

            return false;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModulePipelineRuntimeSystem))]
    public partial struct Space4XModulePipelineMetricsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ModulePipelineMetrics>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info) || !info.ScenarioId.Equals(PipelineConst.ScenarioId) ||
                !SystemAPI.TryGetSingletonEntity<ModulePipelineMetrics>(out var metricsEntity))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var m = state.EntityManager.GetComponentData<ModulePipelineMetrics>(metricsEntity);

            var avgC = PipelineConst.Avg(m.Cq, m.Cc);
            var avgP = PipelineConst.Avg(m.Pq, m.Pc);
            var avgO = PipelineConst.Avg(m.Oq, m.Oc);
            var avgM = PipelineConst.Avg(m.Mq, m.Mc);
            var avgF = PipelineConst.Avg(m.Fq, m.Fc);
            var avgIntegration = PipelineConst.Avg(m.IntegrationSum, m.Assembled);
            var avgInstall = PipelineConst.Avg(m.InstallSum, m.Installed);

            if (time.Tick % 50u == 0u && m.LastSnap != time.Tick)
            {
                m.LastSnap = time.Tick;
                global::UnityEngine.Debug.Log($"[ModulePipelineV0Metrics] tick={time.Tick} modules.limbs.produced.count={m.LimbsProduced} modules.assembled.count={m.Assembled} modules.installed.count={m.Installed} modules.scrap.count={m.Scrap} modules.avg_integration_quality={avgIntegration:0.000} modules.avg_install_quality={avgInstall:0.000} modules.digest={m.Digest}");
                Emit(ref state, in m, avgC, avgP, avgO, avgM, avgF, avgIntegration, avgInstall);
            }

            var endTick = runtime.EndTick;
            if (endTick == 0u && runtime.DurationSeconds > 0f)
            {
                var dt = math.max(1e-4f, time.FixedDeltaTime > 0f ? time.FixedDeltaTime : (1f / 60f));
                endTick = runtime.StartTick + (uint)math.max(1f, math.ceil(runtime.DurationSeconds / dt));
            }

            if (m.Emitted == 0 && endTick > 0u && time.Tick >= endTick)
            {
                Space4XOperatorReportUtility.RequestHeadlessAnswersFlush(ref state, time.Tick);
                Emit(ref state, in m, avgC, avgP, avgO, avgM, avgF, avgIntegration, avgInstall);
                m.Emitted = 1;
                global::UnityEngine.Debug.Log($"[ModulePipelineV0] COMPLETE tick={time.Tick} installed={m.Installed} digest={m.Digest}");
            }

            state.EntityManager.SetComponentData(metricsEntity, m);
        }

        private static void Emit(ref SystemState state, in ModulePipelineMetrics m, float avgC, float avgP, float avgO, float avgM, float avgF, float avgIntegration, float avgInstall)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var b))
            {
                return;
            }

            AddOrUpdate(b, "modules.limbs.produced.count", m.LimbsProduced);
            AddOrUpdate(b, "modules.assembled.count", m.Assembled);
            AddOrUpdate(b, "modules.installed.count", m.Installed);
            AddOrUpdate(b, "modules.scrap.count", m.Scrap);
            AddOrUpdate(b, "modules.avg_limb_quality.cooling", avgC);
            AddOrUpdate(b, "modules.avg_limb_quality.power", avgP);
            AddOrUpdate(b, "modules.avg_limb_quality.optics", avgO);
            AddOrUpdate(b, "modules.avg_limb_quality.mount", avgM);
            AddOrUpdate(b, "modules.avg_limb_quality.firmware", avgF);
            AddOrUpdate(b, "modules.avg_integration_quality", avgIntegration);
            AddOrUpdate(b, "modules.avg_install_quality", avgInstall);
            AddOrUpdate(b, "modules.digest", m.Digest);
        }

        private static void AddOrUpdate(DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
        {
            var fixedKey = new FixedString64Bytes(key);
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(fixedKey))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric { Key = fixedKey, Value = value });
        }
    }
}
