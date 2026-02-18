using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlRoomDirectorSystem))]
    public partial struct Space4XFleetcrawlPurchaseSystem : ISystem
    {
        private const float DamagePurchaseMultiplier = 1.10f;
        private const float CooldownPurchaseMultiplier = 0.90f;
        private const float HealPurchaseRatio = 0.15f;
        private const int DamageBoostCost = 40;
        private const int CooldownTrimCost = 40;
        private const int HealCost = 40;
        private const int RerollTokenCost = 40;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<RunCurrency>();
            state.RequireForUpdate<Space4XRunReactiveModifiers>();
            state.RequireForUpdate<Space4XRunRerollTokens>();
            state.RequireForUpdate<Space4XRunPendingPurchaseRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlDirectorState>(out var directorEntity))
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.HasComponent<Space4XRunPendingPurchaseRequest>(directorEntity))
            {
                return;
            }

            var director = em.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            var request = em.GetComponentData<Space4XRunPendingPurchaseRequest>(directorEntity);
            var currency = em.GetComponentData<RunCurrency>(directorEntity);
            var rerollTokens = em.GetComponentData<Space4XRunRerollTokens>(directorEntity);
            var modifiers = em.GetComponentData<Space4XRunReactiveModifiers>(directorEntity);
            var kind = request.Kind;
            var cost = ResolveCost(kind);
            var roomMatches = request.RoomIndex < 0 || request.RoomIndex == director.CurrentRoomIndex;
            var applied = false;
            var reason = "ok";

            if (!roomMatches)
            {
                reason = "room_mismatch";
            }
            else if (cost <= 0)
            {
                reason = "unknown_offer";
            }
            else if (currency.Value < cost)
            {
                reason = "insufficient_currency";
            }
            else
            {
                applied = ApplyPurchase(ref state, kind, ref modifiers, ref rerollTokens);
                if (applied)
                {
                    currency.Value -= cost;
                }
                else
                {
                    reason = "unknown_offer";
                }
            }

            if (em.HasComponent<Space4XRunPendingPurchaseRequest>(directorEntity))
            {
                em.RemoveComponent<Space4XRunPendingPurchaseRequest>(directorEntity);
            }

            em.SetComponentData(directorEntity, currency);
            em.SetComponentData(directorEntity, rerollTokens);
            em.SetComponentData(directorEntity, modifiers);

            Debug.Log($"[FleetcrawlEconomy] PURCHASE room={request.RoomIndex} node={request.NodeOrdinal} kind={kind} offer={request.PurchaseId} cost={cost} applied={(applied ? 1 : 0)} currency={currency.Value} reason={reason}.");
        }

        private static int ResolveCost(Space4XFleetcrawlPurchaseKind kind)
        {
            switch (kind)
            {
                case Space4XFleetcrawlPurchaseKind.DamageBoost:
                    return DamageBoostCost;
                case Space4XFleetcrawlPurchaseKind.CooldownTrim:
                    return CooldownTrimCost;
                case Space4XFleetcrawlPurchaseKind.Heal:
                    return HealCost;
                case Space4XFleetcrawlPurchaseKind.RerollToken:
                    return RerollTokenCost;
                default:
                    return -1;
            }
        }

        private bool ApplyPurchase(
            ref SystemState state,
            Space4XFleetcrawlPurchaseKind kind,
            ref Space4XRunReactiveModifiers modifiers,
            ref Space4XRunRerollTokens rerollTokens)
        {
            switch (kind)
            {
                case Space4XFleetcrawlPurchaseKind.DamageBoost:
                    modifiers.DamageMul *= DamagePurchaseMultiplier;
                    foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
                    {
                        var buffer = weapons;
                        for (var i = 0; i < buffer.Length; i++)
                        {
                            var mount = buffer[i];
                            mount.Weapon.BaseDamage *= DamagePurchaseMultiplier;
                            buffer[i] = mount;
                        }
                    }
                    return true;

                case Space4XFleetcrawlPurchaseKind.CooldownTrim:
                    modifiers.CooldownMul *= CooldownPurchaseMultiplier;
                    foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
                    {
                        var buffer = weapons;
                        for (var i = 0; i < buffer.Length; i++)
                        {
                            var mount = buffer[i];
                            mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round((float)mount.Weapon.CooldownTicks * CooldownPurchaseMultiplier));
                            buffer[i] = mount;
                        }
                    }
                    return true;

                case Space4XFleetcrawlPurchaseKind.Heal:
                    foreach (var hull in SystemAPI.Query<RefRW<HullIntegrity>>().WithAll<Space4XRunPlayerTag>())
                    {
                        var hullData = hull.ValueRO;
                        hullData.Current = math.min(hullData.Max, hullData.Current + hullData.Max * HealPurchaseRatio);
                        hull.ValueRW = hullData;
                    }
                    return true;

                case Space4XFleetcrawlPurchaseKind.RerollToken:
                    rerollTokens.Value += 1;
                    return true;

                default:
                    return false;
            }
        }
    }
}
