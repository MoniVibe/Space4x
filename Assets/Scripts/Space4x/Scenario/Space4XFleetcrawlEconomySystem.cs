using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Scenario
{
    public enum Space4XFleetcrawlPurchaseKind : byte
    {
        Unknown = 0,
        DamageMultiplier = 1,
        CooldownMultiplier = 2,
        HealHullPercent = 3,
        RerollToken = 4,
        // Legacy aliases kept for compatibility with existing UI bridge/data paths.
        DamageBoost = DamageMultiplier,
        CooldownTrim = CooldownMultiplier,
        Heal = HealHullPercent
    }

    public struct Space4XFleetcrawlPurchaseRequest : IComponentData
    {
        public int RoomIndex;
        public Space4XFleetcrawlPurchaseKind Kind;
        public FixedString64Bytes OfferId;
        public int Cost;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlRoomDirectorSystem))]
    public partial struct Space4XFleetcrawlPurchaseSystem : ISystem
    {
        private const float DamagePurchaseMultiplier = 1.10f;
        private const float CooldownPurchaseMultiplier = 0.90f;
        private const float HealPurchaseRatio = 0.15f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<Space4XFleetcrawlPurchaseRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlDirectorState>(out var directorEntity))
            {
                return;
            }

            var em = state.EntityManager;
            var director = em.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            var currency = em.GetComponentData<RunCurrency>(directorEntity);
            var rerollTokens = em.GetComponentData<Space4XRunRerollTokens>(directorEntity);
            var modifiers = em.GetComponentData<Space4XRunReactiveModifiers>(directorEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (requestRef, requestEntity) in SystemAPI.Query<RefRO<Space4XFleetcrawlPurchaseRequest>>().WithEntityAccess())
            {
                var request = requestRef.ValueRO;
                var kind = ResolveKind(request);
                var cost = math.max(0, request.Cost);
                var roomMatches = request.RoomIndex < 0 || request.RoomIndex == director.CurrentRoomIndex;
                var canAfford = currency.Value >= cost;
                var applied = false;
                var reason = "ok";

                if (!roomMatches)
                {
                    reason = "room_mismatch";
                }
                else if (!canAfford)
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

                ecb.RemoveComponent<Space4XFleetcrawlPurchaseRequest>(requestEntity);
                Debug.Log($"[FleetcrawlEconomy] PURCHASE room={request.RoomIndex} kind={kind} offer={request.OfferId} cost={cost} applied={(applied ? 1 : 0)} currency={currency.Value} reason={reason}.");
            }

            em.SetComponentData(directorEntity, currency);
            em.SetComponentData(directorEntity, rerollTokens);
            em.SetComponentData(directorEntity, modifiers);

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static Space4XFleetcrawlPurchaseKind ResolveKind(in Space4XFleetcrawlPurchaseRequest request)
        {
            if (request.Kind != Space4XFleetcrawlPurchaseKind.Unknown)
            {
                return request.Kind;
            }

            if (request.OfferId.Equals(new FixedString64Bytes("purchase_damage_multiplier")))
            {
                return Space4XFleetcrawlPurchaseKind.DamageMultiplier;
            }

            if (request.OfferId.Equals(new FixedString64Bytes("purchase_cooldown_multiplier")))
            {
                return Space4XFleetcrawlPurchaseKind.CooldownMultiplier;
            }

            if (request.OfferId.Equals(new FixedString64Bytes("purchase_heal_hull_percent")))
            {
                return Space4XFleetcrawlPurchaseKind.HealHullPercent;
            }

            if (request.OfferId.Equals(new FixedString64Bytes("purchase_reroll_token")))
            {
                return Space4XFleetcrawlPurchaseKind.RerollToken;
            }

            return Space4XFleetcrawlPurchaseKind.Unknown;
        }

        private bool ApplyPurchase(
            ref SystemState state,
            Space4XFleetcrawlPurchaseKind kind,
            ref Space4XRunReactiveModifiers modifiers,
            ref Space4XRunRerollTokens rerollTokens)
        {
            switch (kind)
            {
                case Space4XFleetcrawlPurchaseKind.DamageMultiplier:
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

                case Space4XFleetcrawlPurchaseKind.CooldownMultiplier:
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

                case Space4XFleetcrawlPurchaseKind.HealHullPercent:
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
