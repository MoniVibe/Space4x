using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Scenario
{
    public struct Space4XRunPendingGatePick : IComponentData
    {
        public int RoomIndex;
        public int GateOrdinal;
    }

    public struct Space4XRunPendingBoonPick : IComponentData
    {
        public int RoomIndex;
        public int OfferIndex;
    }

    public struct Space4XRunPendingPurchaseRequest : IComponentData
    {
        public int RoomIndex;
        public int NodeOrdinal;
        public Space4XFleetcrawlPurchaseKind Kind;
        public FixedString64Bytes PurchaseId;
    }

    public struct Space4XFleetcrawlOfferPreview
    {
        public Space4XRunRewardKind RewardKind;
        public Space4XRunBlueprintKind BlueprintKind;
        public FixedString64Bytes RewardId;
    }

    public struct Space4XFleetcrawlBlueprintDefinition
    {
        public FixedString64Bytes BlueprintId;
        public FixedString64Bytes BaseModuleId;
        public FixedString64Bytes ManufacturerId;
        public FixedString64Bytes PartA;
        public FixedString64Bytes PartB;
        public Space4XRunBlueprintKind Kind;
    }

    public enum Space4XFleetcrawlInputMode : byte
    {
        Auto = 0,
        Manual = 1
    }

    public struct Space4XFleetcrawlPlayerDirective : IComponentData
    {
        public float2 Movement;
        public byte BoostRequested;
        public byte DashRequested;
        public byte SpecialRequested;
        public uint Tick;
        public uint BoostCooldownUntilTick;
        public uint DashCooldownUntilTick;
        public uint SpecialCooldownUntilTick;
    }

    internal static class Space4XFleetcrawlUiBridge
    {
        public static bool IsFleetcrawlScenario(in FixedString64Bytes scenarioId)
        {
            return scenarioId.Length > 0 && scenarioId.ToString().StartsWith("space4x_fleetcrawl", System.StringComparison.OrdinalIgnoreCase);
        }

        public static Space4XFleetcrawlInputMode ReadInputMode()
        {
            var raw = Environment.GetEnvironmentVariable("SPACE4X_FLEETCRAWL_INPUT_MODE");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Space4XFleetcrawlInputMode.Manual;
            }

            return raw.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? Space4XFleetcrawlInputMode.Auto
                : Space4XFleetcrawlInputMode.Manual;
        }

        public static uint ReadTicksSetting(string envName, uint defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(raw) || !uint.TryParse(raw.Trim(), out var value))
            {
                return defaultValue;
            }

            return value;
        }

        public static bool TryReadPickIndex(string envName, out int index)
        {
            index = 0;
            var raw = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var parsed))
            {
                return false;
            }

            // Accept both 0-based and 1-based user inputs.
            if (parsed > 0 && parsed <= 3)
            {
                index = parsed - 1;
                return true;
            }

            index = parsed;
            return true;
        }

        public static void ClearPickEnv(string envName)
        {
            Environment.SetEnvironmentVariable(envName, string.Empty);
        }

        public static int ResolveGateCount(Space4XFleetcrawlRoomKind roomKind)
        {
            return roomKind == Space4XFleetcrawlRoomKind.Relief ? 2 : 3;
        }

        public static Space4XRunGateKind ResolveGateKind(Space4XFleetcrawlRoomKind roomKind, int gateOrdinal)
        {
            if (roomKind == Space4XFleetcrawlRoomKind.Relief)
            {
                return gateOrdinal == 0 ? Space4XRunGateKind.Boon : Space4XRunGateKind.Blueprint;
            }

            return gateOrdinal switch
            {
                0 => Space4XRunGateKind.Boon,
                1 => Space4XRunGateKind.Blueprint,
                _ => Space4XRunGateKind.Relief
            };
        }

        public static int ResolveAutoGateOrdinal(uint seed, int roomIndex, int gateCount)
        {
            if (gateCount <= 1)
            {
                return 0;
            }

            var hash = DeterministicMix(seed, (uint)(roomIndex + 1), 0x4F1BBCDDu, 0x7F4A7C15u);
            return (int)(hash % (uint)gateCount);
        }

        public static int ResolveAutoOfferIndex(uint seed, int roomIndex, Space4XRunGateKind gateKind, int offerCount)
        {
            if (offerCount <= 1)
            {
                return 0;
            }

            var hash = DeterministicMix(seed, (uint)(roomIndex + 1), (uint)gateKind + 101u, 0xC3A5C85Cu);
            return (int)(hash % (uint)offerCount);
        }

        public static FixedString64Bytes ResolveBoonOfferIdAt(uint seed, int roomIndex, int offerIndex)
        {
            ResolveGateOffers(seed, roomIndex, Space4XRunGateKind.Boon, out var offerA, out var offerB, out var offerC);
            var clampedIndex = math.clamp(offerIndex, 0, 2);
            return clampedIndex switch
            {
                0 => offerA.RewardId,
                1 => offerB.RewardId,
                _ => offerC.RewardId
            };
        }

        public static void ResolveGateOffers(
            uint seed,
            int roomIndex,
            Space4XRunGateKind gateKind,
            out Space4XFleetcrawlOfferPreview offerA,
            out Space4XFleetcrawlOfferPreview offerB,
            out Space4XFleetcrawlOfferPreview offerC)
        {
            var start = (int)(DeterministicMix(seed, (uint)(roomIndex + 1), (uint)gateKind + 17u, 0xA5A5A5A5u) % 4u);
            switch (gateKind)
            {
                case Space4XRunGateKind.Boon:
                    offerA = GetBoonOffer(start % 4);
                    offerB = GetBoonOffer((start + 1) % 4);
                    offerC = GetBoonOffer((start + 2) % 4);
                    break;
                case Space4XRunGateKind.Blueprint:
                    offerA = GetBlueprintOffer(start % 4);
                    offerB = GetBlueprintOffer((start + 1) % 4);
                    offerC = GetBlueprintOffer((start + 2) % 4);
                    break;
                default:
                    offerA = GetReliefOffer(0);
                    offerB = GetReliefOffer(1);
                    offerC = GetReliefOffer(2);
                    break;
            }
        }

        public static Space4XFleetcrawlOfferPreview ResolvePickedOffer(
            uint seed,
            int roomIndex,
            Space4XRunGateKind gateKind,
            int offerIndex)
        {
            ResolveGateOffers(seed, roomIndex, gateKind, out var offerA, out var offerB, out var offerC);
            return math.clamp(offerIndex, 0, 2) switch
            {
                0 => offerA,
                1 => offerB,
                _ => offerC
            };
        }

        public static string DescribeOffer(in Space4XFleetcrawlOfferPreview offer)
        {
            return offer.RewardKind switch
            {
                Space4XRunRewardKind.Boon => $"Boon: {DescribePerk(offer.RewardId)}",
                Space4XRunRewardKind.ModuleBlueprint => $"Blueprint: {DescribeBlueprint(offer.RewardId)}",
                Space4XRunRewardKind.Currency => "Relief: Currency cache (+35).",
                Space4XRunRewardKind.Heal => "Relief: Hull patch (+8%).",
                Space4XRunRewardKind.Reroll => "Relief: Reroll token (+1).",
                _ => offer.RewardId.ToString()
            };
        }

        public static string DescribePerk(in FixedString64Bytes perkId)
        {
            if (perkId.Equals(new FixedString64Bytes("perk_beam_chain_small")))
            {
                return "Beam hits chain to 1 nearby target (low chance).";
            }
            if (perkId.Equals(new FixedString64Bytes("perk_convert_kinetic_to_beam_100")))
            {
                return "All kinetic damage converts to beam.";
            }
            if (perkId.Equals(new FixedString64Bytes("perk_drones_use_beam")))
            {
                return "Drones swap to beam weapon family.";
            }
            if (perkId.Equals(new FixedString64Bytes("perk_beam_damage_mult_small")))
            {
                return "Beam damage multiplier (small stack).";
            }

            return perkId.ToString();
        }

        public static string DescribeBlueprint(in FixedString64Bytes blueprintId)
        {
            if (blueprintId.Equals(new FixedString64Bytes("weapon_laser_prismworks_coreA_lensBeam")))
            {
                return "Weapon laser build: Prismworks beam bias.";
            }
            if (blueprintId.Equals(new FixedString64Bytes("weapon_kinetic_baseline_coreB_barrelKinetic")))
            {
                return "Weapon kinetic build: baseline kinetic output.";
            }
            if (blueprintId.Equals(new FixedString64Bytes("reactor_prismworks_coreA_coolingStable")))
            {
                return "Reactor build: higher damage and better cooldowns.";
            }
            if (blueprintId.Equals(new FixedString64Bytes("hangar_prismworks_guidanceDroneLink_lensBeam")))
            {
                return "Hangar build: drone wing reinforcement.";
            }

            return blueprintId.ToString();
        }

        public static string DescribePurchase(Space4XFleetcrawlPurchaseKind kind)
        {
            return kind switch
            {
                Space4XFleetcrawlPurchaseKind.DamageBoost => "Damage+",
                Space4XFleetcrawlPurchaseKind.CooldownTrim => "Cooldown-",
                Space4XFleetcrawlPurchaseKind.Heal => "Heal",
                Space4XFleetcrawlPurchaseKind.RerollToken => "Reroll token",
                _ => kind.ToString()
            };
        }

        public static bool TryResolveBlueprintDefinition(string blueprintId, out Space4XFleetcrawlBlueprintDefinition definition)
        {
            var normalized = string.IsNullOrWhiteSpace(blueprintId) ? string.Empty : blueprintId.Trim();
            return TryResolveBlueprintDefinition(new FixedString64Bytes(normalized), out definition);
        }

        public static bool TryResolveBlueprintDefinition(
            in FixedString64Bytes blueprintId,
            out Space4XFleetcrawlBlueprintDefinition definition)
        {
            if (blueprintId.Equals(new FixedString64Bytes("weapon_laser_prismworks_coreA_lensBeam")))
            {
                definition = new Space4XFleetcrawlBlueprintDefinition
                {
                    BlueprintId = blueprintId,
                    BaseModuleId = new FixedString64Bytes("weapon_laser"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("coreA"),
                    PartB = new FixedString64Bytes("lensBeam"),
                    Kind = Space4XRunBlueprintKind.Weapon
                };
                return true;
            }

            if (blueprintId.Equals(new FixedString64Bytes("weapon_kinetic_baseline_coreB_barrelKinetic")))
            {
                definition = new Space4XFleetcrawlBlueprintDefinition
                {
                    BlueprintId = blueprintId,
                    BaseModuleId = new FixedString64Bytes("weapon_kinetic"),
                    ManufacturerId = new FixedString64Bytes("baseline"),
                    PartA = new FixedString64Bytes("coreB"),
                    PartB = new FixedString64Bytes("barrelKinetic"),
                    Kind = Space4XRunBlueprintKind.Weapon
                };
                return true;
            }

            if (blueprintId.Equals(new FixedString64Bytes("reactor_prismworks_coreA_coolingStable")))
            {
                definition = new Space4XFleetcrawlBlueprintDefinition
                {
                    BlueprintId = blueprintId,
                    BaseModuleId = new FixedString64Bytes("reactor"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("coreA"),
                    PartB = new FixedString64Bytes("coolingStable"),
                    Kind = Space4XRunBlueprintKind.Reactor
                };
                return true;
            }

            if (blueprintId.Equals(new FixedString64Bytes("hangar_prismworks_guidanceDroneLink_lensBeam")))
            {
                definition = new Space4XFleetcrawlBlueprintDefinition
                {
                    BlueprintId = blueprintId,
                    BaseModuleId = new FixedString64Bytes("hangar"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("guidanceDroneLink"),
                    PartB = new FixedString64Bytes("lensBeam"),
                    Kind = Space4XRunBlueprintKind.Hangar
                };
                return true;
            }

            definition = default;
            return false;
        }

        public static Space4XRunInstalledBlueprint ToInstalledBlueprint(
            in Space4XFleetcrawlBlueprintDefinition definition,
            byte version)
        {
            return new Space4XRunInstalledBlueprint
            {
                BlueprintId = definition.BlueprintId,
                BaseModuleId = definition.BaseModuleId,
                ManufacturerId = definition.ManufacturerId,
                PartA = definition.PartA,
                PartB = definition.PartB,
                Kind = definition.Kind,
                Version = version
            };
        }

        private static Space4XFleetcrawlOfferPreview GetBoonOffer(int index)
        {
            return index switch
            {
                0 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Boon,
                    RewardId = new FixedString64Bytes("perk_convert_kinetic_to_beam_100")
                },
                1 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Boon,
                    RewardId = new FixedString64Bytes("perk_drones_use_beam")
                },
                2 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Boon,
                    RewardId = new FixedString64Bytes("perk_beam_chain_small")
                },
                _ => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Boon,
                    RewardId = new FixedString64Bytes("perk_beam_damage_mult_small")
                }
            };
        }

        private static Space4XFleetcrawlOfferPreview GetBlueprintOffer(int index)
        {
            return index switch
            {
                0 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Weapon,
                    RewardId = new FixedString64Bytes("weapon_laser_prismworks_coreA_lensBeam")
                },
                1 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Weapon,
                    RewardId = new FixedString64Bytes("weapon_kinetic_baseline_coreB_barrelKinetic")
                },
                2 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Reactor,
                    RewardId = new FixedString64Bytes("reactor_prismworks_coreA_coolingStable")
                },
                _ => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Hangar,
                    RewardId = new FixedString64Bytes("hangar_prismworks_guidanceDroneLink_lensBeam")
                }
            };
        }

        private static Space4XFleetcrawlOfferPreview GetReliefOffer(int index)
        {
            return index switch
            {
                0 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Currency,
                    RewardId = new FixedString64Bytes("relief_currency_cache")
                },
                1 => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Heal,
                    RewardId = new FixedString64Bytes("relief_hull_patch")
                },
                _ => new Space4XFleetcrawlOfferPreview
                {
                    RewardKind = Space4XRunRewardKind.Reroll,
                    RewardId = new FixedString64Bytes("relief_reroll_token")
                }
            };
        }

        private static uint DeterministicMix(uint a, uint b, uint c, uint d)
        {
            var hash = 2166136261u;
            hash ^= a;
            hash *= 16777619u;
            hash ^= b;
            hash *= 16777619u;
            hash ^= c;
            hash *= 16777619u;
            hash ^= d;
            hash *= 16777619u;
            return hash;
        }
    }
}
