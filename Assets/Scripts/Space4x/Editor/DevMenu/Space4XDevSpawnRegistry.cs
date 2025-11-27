using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Editor.DevMenu
{
    /// <summary>
    /// Registry of spawnable entity templates for the dev menu.
    /// Provides a catalog of all entity types that can be spawned at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Space4XDevSpawnRegistry", menuName = "Space4X/Dev/Spawn Registry")]
    public class Space4XDevSpawnRegistry : ScriptableObject
    {
        [Header("Carriers")]
        public CarrierTemplate[] carriers = new CarrierTemplate[]
        {
            new CarrierTemplate
            {
                id = "carrier_light",
                displayName = "Light Carrier",
                description = "Fast carrier with limited hangar capacity",
                speed = 8f,
                hangarCapacity = 12,
                shieldStrength = 500f,
                hullPoints = 1000f,
                color = new Color(0.3f, 0.5f, 0.8f)
            },
            new CarrierTemplate
            {
                id = "carrier_medium",
                displayName = "Fleet Carrier",
                description = "Standard carrier with balanced stats",
                speed = 5f,
                hangarCapacity = 24,
                shieldStrength = 1000f,
                hullPoints = 2000f,
                color = new Color(0.4f, 0.4f, 0.7f)
            },
            new CarrierTemplate
            {
                id = "carrier_heavy",
                displayName = "Super Carrier",
                description = "Massive carrier with extensive hangar bays",
                speed = 3f,
                hangarCapacity = 48,
                shieldStrength = 2000f,
                hullPoints = 5000f,
                color = new Color(0.5f, 0.3f, 0.6f)
            }
        };

        [Header("Capital Ships")]
        public CapitalShipTemplate[] capitalShips = new CapitalShipTemplate[]
        {
            new CapitalShipTemplate
            {
                id = "frigate",
                displayName = "Frigate",
                description = "Light escort vessel",
                speed = 12f,
                shieldStrength = 200f,
                hullPoints = 400f,
                weaponDamage = 25f,
                weaponRange = 50f,
                color = new Color(0.6f, 0.6f, 0.6f)
            },
            new CapitalShipTemplate
            {
                id = "destroyer",
                displayName = "Destroyer",
                description = "Anti-fighter combat ship",
                speed = 10f,
                shieldStrength = 400f,
                hullPoints = 600f,
                weaponDamage = 40f,
                weaponRange = 60f,
                color = new Color(0.5f, 0.5f, 0.5f)
            },
            new CapitalShipTemplate
            {
                id = "cruiser",
                displayName = "Cruiser",
                description = "Multi-role combat vessel",
                speed = 7f,
                shieldStrength = 800f,
                hullPoints = 1200f,
                weaponDamage = 60f,
                weaponRange = 80f,
                color = new Color(0.4f, 0.4f, 0.5f)
            },
            new CapitalShipTemplate
            {
                id = "battleship",
                displayName = "Battleship",
                description = "Heavy line-of-battle ship",
                speed = 4f,
                shieldStrength = 1500f,
                hullPoints = 3000f,
                weaponDamage = 100f,
                weaponRange = 100f,
                color = new Color(0.3f, 0.3f, 0.4f)
            },
            new CapitalShipTemplate
            {
                id = "dreadnought",
                displayName = "Dreadnought",
                description = "Massive warship, no hangar bays",
                speed = 2f,
                shieldStrength = 3000f,
                hullPoints = 6000f,
                weaponDamage = 200f,
                weaponRange = 120f,
                color = new Color(0.2f, 0.2f, 0.3f)
            }
        };

        [Header("Strike Craft")]
        public StrikeCraftTemplate[] strikeCraft = new StrikeCraftTemplate[]
        {
            new StrikeCraftTemplate
            {
                id = "fighter",
                displayName = "Fighter",
                description = "Fast interceptor",
                speed = 25f,
                hullPoints = 50f,
                weaponDamage = 10f,
                weaponRange = 20f,
                recallThresholdFuel = 0.2f,
                recallThresholdAmmo = 0.1f,
                role = StrikeCraftRoleType.Interceptor,
                color = new Color(0.2f, 0.7f, 0.3f)
            },
            new StrikeCraftTemplate
            {
                id = "bomber",
                displayName = "Bomber",
                description = "Anti-capital strike craft",
                speed = 15f,
                hullPoints = 80f,
                weaponDamage = 50f,
                weaponRange = 15f,
                recallThresholdFuel = 0.25f,
                recallThresholdAmmo = 0.15f,
                role = StrikeCraftRoleType.Bomber,
                color = new Color(0.7f, 0.3f, 0.2f)
            },
            new StrikeCraftTemplate
            {
                id = "interceptor",
                displayName = "Heavy Interceptor",
                description = "Elite dogfighter",
                speed = 30f,
                hullPoints = 40f,
                weaponDamage = 15f,
                weaponRange = 25f,
                recallThresholdFuel = 0.15f,
                recallThresholdAmmo = 0.1f,
                role = StrikeCraftRoleType.Interceptor,
                color = new Color(0.3f, 0.8f, 0.4f)
            },
            new StrikeCraftTemplate
            {
                id = "gunship",
                displayName = "Gunship",
                description = "Heavy attack craft",
                speed = 12f,
                hullPoints = 120f,
                weaponDamage = 35f,
                weaponRange = 30f,
                recallThresholdFuel = 0.3f,
                recallThresholdAmmo = 0.2f,
                role = StrikeCraftRoleType.Gunship,
                color = new Color(0.6f, 0.4f, 0.2f)
            }
        };

        [Header("Support Vessels")]
        public SupportVesselTemplate[] supportVessels = new SupportVesselTemplate[]
        {
            new SupportVesselTemplate
            {
                id = "miner",
                displayName = "Mining Vessel",
                description = "Resource extraction ship",
                speed = 10f,
                cargoCapacity = 100f,
                miningEfficiency = 0.8f,
                color = new Color(0.6f, 0.5f, 0.3f)
            },
            new SupportVesselTemplate
            {
                id = "hauler",
                displayName = "Cargo Hauler",
                description = "Transport vessel",
                speed = 8f,
                cargoCapacity = 500f,
                miningEfficiency = 0f,
                color = new Color(0.5f, 0.5f, 0.4f)
            },
            new SupportVesselTemplate
            {
                id = "repair_tender",
                displayName = "Repair Tender",
                description = "Field repair vessel",
                speed = 6f,
                cargoCapacity = 50f,
                miningEfficiency = 0f,
                repairRate = 10f,
                color = new Color(0.4f, 0.6f, 0.4f)
            }
        };

        [Header("Stations")]
        public StationTemplate[] stations = new StationTemplate[]
        {
            new StationTemplate
            {
                id = "outpost",
                displayName = "Outpost",
                description = "Small forward base",
                shieldStrength = 500f,
                hullPoints = 2000f,
                dockingCapacity = 4,
                color = new Color(0.5f, 0.5f, 0.5f)
            },
            new StationTemplate
            {
                id = "starbase",
                displayName = "Starbase",
                description = "Major space station",
                shieldStrength = 2000f,
                hullPoints = 10000f,
                dockingCapacity = 12,
                color = new Color(0.4f, 0.4f, 0.5f)
            }
        };

        [Header("Celestial")]
        public CelestialTemplate[] celestials = new CelestialTemplate[]
        {
            new CelestialTemplate
            {
                id = "asteroid_small",
                displayName = "Small Asteroid",
                description = "Minor mineral deposit",
                resourceAmount = 200f,
                miningRate = 5f,
                resourceType = Registry.ResourceType.Minerals,
                scale = 1f,
                color = new Color(0.5f, 0.4f, 0.3f)
            },
            new CelestialTemplate
            {
                id = "asteroid_medium",
                displayName = "Medium Asteroid",
                description = "Standard mineral deposit",
                resourceAmount = 500f,
                miningRate = 10f,
                resourceType = Registry.ResourceType.Minerals,
                scale = 2f,
                color = new Color(0.5f, 0.4f, 0.3f)
            },
            new CelestialTemplate
            {
                id = "asteroid_large",
                displayName = "Large Asteroid",
                description = "Rich mineral deposit",
                resourceAmount = 1000f,
                miningRate = 15f,
                resourceType = Registry.ResourceType.Minerals,
                scale = 3f,
                color = new Color(0.5f, 0.4f, 0.3f)
            },
            new CelestialTemplate
            {
                id = "asteroid_rare",
                displayName = "Rare Metal Asteroid",
                description = "Rare metal deposit",
                resourceAmount = 300f,
                miningRate = 8f,
                resourceType = Registry.ResourceType.RareMetals,
                scale = 1.5f,
                color = new Color(0.6f, 0.5f, 0.7f)
            }
        };

        [Header("Factions")]
        public FactionTemplate[] factions = new FactionTemplate[]
        {
            new FactionTemplate
            {
                id = "player",
                displayName = "Player Fleet",
                isPlayer = true,
                color = new Color(0.2f, 0.5f, 0.8f)
            },
            new FactionTemplate
            {
                id = "enemy_pirates",
                displayName = "Pirate Raiders",
                isPlayer = false,
                hostileToPlayer = true,
                color = new Color(0.8f, 0.2f, 0.2f)
            },
            new FactionTemplate
            {
                id = "neutral_traders",
                displayName = "Trade Consortium",
                isPlayer = false,
                hostileToPlayer = false,
                color = new Color(0.8f, 0.8f, 0.2f)
            }
        };

        // Enums
        public enum StrikeCraftRoleType : byte
        {
            Interceptor = 0,
            Bomber = 1,
            Gunship = 2,
            Scout = 3
        }

        // Templates
        [Serializable]
        public struct CarrierTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float speed;
            public int hangarCapacity;
            public float shieldStrength;
            public float hullPoints;
            public Color color;
        }

        [Serializable]
        public struct CapitalShipTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float speed;
            public float shieldStrength;
            public float hullPoints;
            public float weaponDamage;
            public float weaponRange;
            public Color color;
        }

        [Serializable]
        public struct StrikeCraftTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float speed;
            public float hullPoints;
            public float weaponDamage;
            public float weaponRange;
            public float recallThresholdFuel;
            public float recallThresholdAmmo;
            public StrikeCraftRoleType role;
            public Color color;
        }

        [Serializable]
        public struct SupportVesselTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float speed;
            public float cargoCapacity;
            public float miningEfficiency;
            public float repairRate;
            public Color color;
        }

        [Serializable]
        public struct StationTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float shieldStrength;
            public float hullPoints;
            public int dockingCapacity;
            public Color color;
        }

        [Serializable]
        public struct CelestialTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public float resourceAmount;
            public float miningRate;
            public Registry.ResourceType resourceType;
            public float scale;
            public Color color;
        }

        [Serializable]
        public struct FactionTemplate
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public bool isPlayer;
            public bool hostileToPlayer;
            public Color color;
        }

        // Helper methods
        public CarrierTemplate? GetCarrier(string id)
        {
            foreach (var c in carriers)
                if (c.id == id) return c;
            return null;
        }

        public CapitalShipTemplate? GetCapitalShip(string id)
        {
            foreach (var c in capitalShips)
                if (c.id == id) return c;
            return null;
        }

        public StrikeCraftTemplate? GetStrikeCraft(string id)
        {
            foreach (var s in strikeCraft)
                if (s.id == id) return s;
            return null;
        }

        public SupportVesselTemplate? GetSupportVessel(string id)
        {
            foreach (var s in supportVessels)
                if (s.id == id) return s;
            return null;
        }

        public StationTemplate? GetStation(string id)
        {
            foreach (var s in stations)
                if (s.id == id) return s;
            return null;
        }

        public CelestialTemplate? GetCelestial(string id)
        {
            foreach (var c in celestials)
                if (c.id == id) return c;
            return null;
        }

        public FactionTemplate? GetFaction(string id)
        {
            foreach (var f in factions)
                if (f.id == id) return f;
            return null;
        }

        /// <summary>
        /// Gets all spawnable categories for menu building.
        /// </summary>
        public string[] GetCategories()
        {
            return new[] { "Carriers", "Capital Ships", "Strike Craft", "Support Vessels", "Stations", "Celestial", "Factions" };
        }

        /// <summary>
        /// Gets all templates in a category.
        /// </summary>
        public (string id, string displayName, string description)[] GetTemplatesInCategory(string category)
        {
            var result = new List<(string, string, string)>();

            switch (category)
            {
                case "Carriers":
                    foreach (var t in carriers)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Capital Ships":
                    foreach (var t in capitalShips)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Strike Craft":
                    foreach (var t in strikeCraft)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Support Vessels":
                    foreach (var t in supportVessels)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Stations":
                    foreach (var t in stations)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Celestial":
                    foreach (var t in celestials)
                        result.Add((t.id, t.displayName, t.description));
                    break;
                case "Factions":
                    foreach (var t in factions)
                        result.Add((t.id, t.displayName, string.Empty));
                    break;
            }

            return result.ToArray();
        }
    }
}

