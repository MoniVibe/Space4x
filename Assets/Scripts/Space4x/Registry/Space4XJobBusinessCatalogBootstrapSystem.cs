using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XJobBusinessCatalogBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XJobCatalogSingleton>(out _))
            {
                CreateJobCatalog(ref state);
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XBusinessCatalogSingleton>(out _))
            {
                CreateBusinessCatalog(ref state);
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var catalogRef in SystemAPI.Query<RefRW<Space4XJobCatalogSingleton>>())
            {
                if (catalogRef.ValueRO.Catalog.IsCreated)
                {
                    catalogRef.ValueRO.Catalog.Dispose();
                    catalogRef.ValueRW.Catalog = default;
                }
            }

            foreach (var catalogRef in SystemAPI.Query<RefRW<Space4XBusinessCatalogSingleton>>())
            {
                if (catalogRef.ValueRO.Catalog.IsCreated)
                {
                    catalogRef.ValueRO.Catalog.Dispose();
                    catalogRef.ValueRW.Catalog = default;
                }
            }
        }

        private static void CreateJobCatalog(ref SystemState state)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<Space4XJobCatalogBlob>();
            var jobs = builder.Allocate(ref root.Jobs, 25);

            ConfigureJob(
                ref builder,
                ref jobs[0],
                "job_mining_basic",
                "Mining (Ore)",
                Space4XJobKind.Mining,
                FacilityBusinessClass.None,
                0,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.5f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Ore, 6f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[1],
                "job_refining_ore",
                "Refining (Ore -> Minerals)",
                Space4XJobKind.Refining,
                FacilityBusinessClass.Refinery,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Ore, 6f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 4f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[2],
                "job_hauling_contract",
                "Hauling Contract",
                Space4XJobKind.Hauling,
                FacilityBusinessClass.None,
                0,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.4f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.1f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[3],
                "job_repair_service",
                "Repair Service",
                Space4XJobKind.Repair,
                FacilityBusinessClass.Shipyard,
                1,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 1f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[4],
                "job_survey_anomaly",
                "Survey Anomaly",
                Space4XJobKind.Survey,
                FacilityBusinessClass.Research,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.3f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.RelicData, 0.5f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[5],
                "job_patrol_lane",
                "Patrol Lane",
                Space4XJobKind.Patrol,
                FacilityBusinessClass.None,
                0,
                10,
                0.15f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.5f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.3f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[6],
                "job_trade_brokerage",
                "Trade Brokerage",
                Space4XJobKind.TradeBroker,
                FacilityBusinessClass.Production,
                1,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.1f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[7],
                "job_construction_outpost",
                "Construction (Outpost)",
                Space4XJobKind.Construction,
                FacilityBusinessClass.Construction,
                2,
                10,
                0.2f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 8f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 4f),
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 1f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[8],
                "job_salvage_recovery",
                "Salvage Recovery",
                Space4XJobKind.Salvage,
                FacilityBusinessClass.None,
                0,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.3f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.SalvageComponents, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[9],
                "job_security_detail",
                "Security Detail",
                Space4XJobKind.Security,
                FacilityBusinessClass.None,
                0,
                10,
                0.2f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.2f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                null);

            ConfigureJob(
                ref builder,
                ref jobs[10],
                "job_fuel_refining",
                "Fuel Refining (Volatiles)",
                Space4XJobKind.Refining,
                FacilityBusinessClass.Refinery,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Volatiles, 4f),
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 1f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 3f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[11],
                "job_food_processing",
                "Bio Processing (Food)",
                Space4XJobKind.Production,
                FacilityBusinessClass.Production,
                0,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.OrganicMatter, 3f),
                    Space4XJobResourceSpec.Create(ResourceType.Water, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Food, 4f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[12],
                "job_salvage_sorting",
                "Salvage Sorting",
                Space4XJobKind.Salvage,
                FacilityBusinessClass.None,
                0,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.SalvageComponents, 3f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.RareMetals, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.IndustrialCrystals, 1f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[13],
                "job_exotic_gas_capture",
                "Exotic Gas Capture",
                Space4XJobKind.Mining,
                FacilityBusinessClass.None,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.4f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.ExoticGases, 1f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[14],
                "job_crystal_fabrication",
                "Crystal Fabrication",
                Space4XJobKind.Production,
                FacilityBusinessClass.ModuleFacility,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.IndustrialCrystals, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[15],
                "job_isotope_distill",
                "Isotope Distill (Ice)",
                Space4XJobKind.Refining,
                FacilityBusinessClass.Refinery,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.HeavyWater, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.LiquidOzone, 1f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Isotopes, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.Water, 1f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[16],
                "job_volatiles_extraction",
                "Volatiles Extraction",
                Space4XJobKind.Mining,
                FacilityBusinessClass.None,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.4f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Volatiles, 4f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[17],
                "job_transplutonic_mining",
                "Deep-Core Mining (Transplutonic)",
                Space4XJobKind.Mining,
                FacilityBusinessClass.None,
                2,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.6f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.4f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.TransplutonicOre, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[18],
                "job_mote_harvesting",
                "Volatile Mote Harvest",
                Space4XJobKind.Mining,
                FacilityBusinessClass.None,
                2,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Fuel, 0.5f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.3f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.VolatileMotes, 1.5f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[19],
                "job_energy_crystal_growing",
                "Energy Crystal Growing",
                Space4XJobKind.Production,
                FacilityBusinessClass.Production,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.Volatiles, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.2f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[20],
                "job_heavywater_processing",
                "Heavy Water Processing",
                Space4XJobKind.Refining,
                FacilityBusinessClass.Refinery,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Water, 3f),
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 0.5f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.HeavyWater, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[21],
                "job_ozone_refining",
                "Liquid Ozone Refining",
                Space4XJobKind.Refining,
                FacilityBusinessClass.Refinery,
                1,
                10,
                0.05f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Volatiles, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 0.5f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.LiquidOzone, 1.5f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[22],
                "job_strontium_synthesis",
                "Strontium Synthesis",
                Space4XJobKind.Production,
                FacilityBusinessClass.Production,
                2,
                10,
                0.1f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 3f),
                    Space4XJobResourceSpec.Create(ResourceType.Volatiles, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 0.3f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.StrontiumClathrates, 1f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[23],
                "job_supplies_fabrication",
                "Supplies Fabrication",
                Space4XJobKind.Production,
                FacilityBusinessClass.Production,
                0,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Minerals, 2f),
                    Space4XJobResourceSpec.Create(ResourceType.OrganicMatter, 1f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Supplies, 2f)
                });

            ConfigureJob(
                ref builder,
                ref jobs[24],
                "job_water_recycling",
                "Water Recycling",
                Space4XJobKind.Production,
                FacilityBusinessClass.Production,
                0,
                10,
                0.0f,
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.OrganicMatter, 1f),
                    Space4XJobResourceSpec.Create(ResourceType.EnergyCrystals, 0.3f)
                },
                new[]
                {
                    Space4XJobResourceSpec.Create(ResourceType.Water, 2f)
                });

            var blob = builder.CreateBlobAssetReference<Space4XJobCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity(typeof(Space4XJobCatalogSingleton));
            state.EntityManager.SetComponentData(entity, new Space4XJobCatalogSingleton { Catalog = blob });
            builder.Dispose();
        }

        private static void CreateBusinessCatalog(ref SystemState state)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<Space4XBusinessCatalogBlob>();
            var businesses = builder.Allocate(ref root.Businesses, 10);

            ConfigureBusiness(
                ref builder,
                ref businesses[0],
                "biz_mining_co",
                "Mining Co",
                Space4XBusinessKind.MiningCompany,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.None,
                600f,
                new[]
                {
                    "job_mining_basic",
                    "job_survey_anomaly",
                    "job_exotic_gas_capture",
                    "job_volatiles_extraction",
                    "job_transplutonic_mining",
                    "job_mote_harvesting",
                    "job_fuel_refining",
                    "job_isotope_distill"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[1],
                "biz_haulage_co",
                "Haulage Co",
                Space4XBusinessKind.HaulageCompany,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.None,
                550f,
                new[]
                {
                    "job_hauling_contract",
                    "job_patrol_lane"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[2],
                "biz_shipwright",
                "Shipwright",
                Space4XBusinessKind.Shipwright,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.Shipyard,
                900f,
                new[]
                {
                    "job_repair_service",
                    "job_construction_outpost",
                    "job_crystal_fabrication"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[3],
                "biz_station_services",
                "Station Services",
                Space4XBusinessKind.StationServices,
                Space4XBusinessOwnerKind.Individual,
                FacilityBusinessClass.Production,
                450f,
                new[]
                {
                    "job_trade_brokerage",
                    "job_repair_service",
                    "job_food_processing",
                    "job_supplies_fabrication"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[4],
                "biz_market_hub",
                "Market Hub",
                Space4XBusinessKind.MarketHub,
                Space4XBusinessOwnerKind.Faction,
                FacilityBusinessClass.Production,
                1200f,
                new[]
                {
                    "job_trade_brokerage",
                    "job_hauling_contract",
                    "job_food_processing",
                    "job_supplies_fabrication",
                    "job_water_recycling"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[5],
                "biz_salvage_crew",
                "Salvage Crew",
                Space4XBusinessKind.SalvageCrew,
                Space4XBusinessOwnerKind.Individual,
                FacilityBusinessClass.None,
                400f,
                new[]
                {
                    "job_salvage_recovery",
                    "job_repair_service",
                    "job_salvage_sorting"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[6],
                "biz_agriplex",
                "Agriplex",
                Space4XBusinessKind.Agriplex,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.Production,
                520f,
                new[]
                {
                    "job_food_processing",
                    "job_water_recycling",
                    "job_supplies_fabrication"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[7],
                "biz_fuel_works",
                "Fuel Works",
                Space4XBusinessKind.FuelWorks,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.Refinery,
                720f,
                new[]
                {
                    "job_fuel_refining",
                    "job_heavywater_processing",
                    "job_ozone_refining",
                    "job_isotope_distill"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[8],
                "biz_industrial_foundry",
                "Industrial Foundry",
                Space4XBusinessKind.IndustrialFoundry,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.Production,
                680f,
                new[]
                {
                    "job_energy_crystal_growing",
                    "job_crystal_fabrication",
                    "job_strontium_synthesis",
                    "job_supplies_fabrication"
                });

            ConfigureBusiness(
                ref builder,
                ref businesses[9],
                "biz_deepcore_syndicate",
                "Deepcore Syndicate",
                Space4XBusinessKind.DeepCoreSyndicate,
                Space4XBusinessOwnerKind.Group,
                FacilityBusinessClass.None,
                640f,
                new[]
                {
                    "job_mining_basic",
                    "job_volatiles_extraction",
                    "job_transplutonic_mining",
                    "job_mote_harvesting",
                    "job_exotic_gas_capture"
                });

            var blob = builder.CreateBlobAssetReference<Space4XBusinessCatalogBlob>(Allocator.Persistent);
            var entity = state.EntityManager.CreateEntity(typeof(Space4XBusinessCatalogSingleton));
            state.EntityManager.SetComponentData(entity, new Space4XBusinessCatalogSingleton { Catalog = blob });
            builder.Dispose();
        }

        private static void ConfigureJob(
            ref BlobBuilder builder,
            ref Space4XJobDefinition job,
            string id,
            string name,
            Space4XJobKind kind,
            FacilityBusinessClass facility,
            byte minTech,
            byte durationTicks,
            float standingGate,
            Space4XJobResourceSpec[] inputs,
            Space4XJobResourceSpec[] outputs)
        {
            job = new Space4XJobDefinition
            {
                Id = new FixedString64Bytes(id),
                Name = new FixedString64Bytes(name),
                Kind = kind,
                RequiredFacility = facility,
                MinTechTier = minTech,
                DurationTicks = durationTicks,
                StandingGate = standingGate
            };

            if (inputs != null && inputs.Length > 0)
            {
                var buffer = builder.Allocate(ref job.Inputs, inputs.Length);
                for (int i = 0; i < inputs.Length; i++)
                {
                    buffer[i] = inputs[i];
                }
            }

            if (outputs != null && outputs.Length > 0)
            {
                var buffer = builder.Allocate(ref job.Outputs, outputs.Length);
                for (int i = 0; i < outputs.Length; i++)
                {
                    buffer[i] = outputs[i];
                }
            }
        }

        private static void ConfigureBusiness(
            ref BlobBuilder builder,
            ref Space4XBusinessDefinition business,
            string id,
            string name,
            Space4XBusinessKind kind,
            Space4XBusinessOwnerKind ownerKind,
            FacilityBusinessClass primaryFacility,
            float startingCredits,
            string[] jobIds)
        {
            business = new Space4XBusinessDefinition
            {
                Id = new FixedString64Bytes(id),
                Name = new FixedString64Bytes(name),
                Kind = kind,
                OwnerKind = ownerKind,
                PrimaryFacility = primaryFacility,
                StartingCredits = startingCredits
            };

            if (jobIds != null && jobIds.Length > 0)
            {
                var buffer = builder.Allocate(ref business.JobIds, jobIds.Length);
                for (int i = 0; i < jobIds.Length; i++)
                {
                    buffer[i] = new FixedString64Bytes(jobIds[i]);
                }
            }
        }
    }
}
