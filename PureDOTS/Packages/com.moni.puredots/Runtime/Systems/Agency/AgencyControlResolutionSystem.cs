using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    /// <summary>
    /// Resolves the winning controller per domain by running a simple pressure-vs-resistance contest.
    /// This is intended as a minimal agency kernel to build richer governance/behavior on top of.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup), OrderFirst = true)]
    public partial struct AgencyControlResolutionSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<AgencyModuleTag, AgencySelf>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (self, links, resolved) in SystemAPI
                .Query<RefRO<AgencySelf>, DynamicBuffer<ControlLink>, DynamicBuffer<ResolvedControl>>()
                .WithAll<AgencyModuleTag>())
            {
                resolved.Clear();

                uint domainsMask = 0u;
                for (int i = 0; i < links.Length; i++)
                {
                    domainsMask |= (uint)links[i].Domains;
                }

                while (domainsMask != 0u)
                {
                    int bitIndex = math.tzcnt(domainsMask);
                    uint domainBit = 1u << bitIndex;
                    domainsMask &= ~domainBit;

                    var domain = (AgencyDomain)domainBit;

                    Entity bestController = Entity.Null;
                    float bestScore = float.NegativeInfinity;

                    for (int i = 0; i < links.Length; i++)
                    {
                        var link = links[i];
                        if ((((uint)link.Domains) & domainBit) == 0u)
                        {
                            continue;
                        }

                        float pressure = ComputePressure(link);
                        float resistance = ComputeResistance(self.ValueRO, link);
                        float score = pressure - resistance;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestController = link.Controller;
                        }
                    }

                    resolved.Add(new ResolvedControl
                    {
                        Domain = domain,
                        Controller = bestScore > 0f ? bestController : Entity.Null,
                        Score = bestScore
                    });
                }
            }
        }

        [BurstCompile]
        private static float ComputePressure(in ControlLink link)
        {
            float legitimacy = math.saturate(link.Legitimacy);
            return math.max(0f, link.Pressure) * (1f + legitimacy);
        }

        [BurstCompile]
        private static float ComputeResistance(in AgencySelf self, in ControlLink link)
        {
            float hostility = math.saturate(link.Hostility);
            float consent = math.saturate(link.Consent);
            float baseResistance = math.max(0f, self.BaseResistance);
            float needBoost = math.max(0f, self.SelfNeedUrgency) * hostility;
            float affinity = math.saturate(self.DominationAffinity);

            float resistance = baseResistance + needBoost;
            resistance *= 1f - consent;
            resistance *= 1f - affinity;

            return resistance;
        }
    }
}

