using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor.Generators
{
    public class IndividualEntityGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.individuals == null)
            {
                // Not an error - individual catalog is optional
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Individuals/Captains");
            EnsureDirectory($"{PrefabBasePath}/Individuals/Officers");
            EnsureDirectory($"{PrefabBasePath}/Individuals/Crew");

            bool anyChanged = false;
            foreach (var individualData in catalog.individuals)
            {
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(individualData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Individuals)
                {
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(individualData.id))
                {
                    result.Warnings.Add("Skipping individual with empty ID");
                    continue;
                }

                // Determine folder based on role
                string roleFolder = GetRoleFolder(individualData.role);
                var prefabPath = $"{PrefabBasePath}/Individuals/{roleFolder}/{individualData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (options.DryRun)
                {
                    result.CreatedCount++;
                    anyChanged = true;
                    continue;
                }

                var individualObj = LoadOrCreatePrefab(prefabPath, individualData.id, out bool isNew);

                // Add IndividualIdAuthoring (if it exists) or use a generic ID component
                // For now, we'll add alignment/race/culture directly

                // Add individual stats
                var individualStats = individualObj.GetComponent<IndividualStatsAuthoring>();
                if (individualStats == null) individualStats = individualObj.AddComponent<IndividualStatsAuthoring>();
                individualStats.command = individualData.command;
                individualStats.tactics = individualData.tactics;
                individualStats.logistics = individualData.logistics;
                individualStats.diplomacy = individualData.diplomacy;
                individualStats.engineering = individualData.engineering;
                individualStats.resolve = individualData.resolve;

                // Add Physique/Finesse/Will
                var pfw = individualObj.GetComponent<PhysiqueFinesseWillAuthoring>();
                if (pfw == null) pfw = individualObj.AddComponent<PhysiqueFinesseWillAuthoring>();
                pfw.physique = individualData.physique;
                pfw.finesse = individualData.finesse;
                pfw.will = individualData.will;
                pfw.physiqueInclination = individualData.physiqueInclination;
                pfw.finesseInclination = individualData.finesseInclination;
                pfw.willInclination = individualData.willInclination;

                // Add alignment
                var alignment = individualObj.GetComponent<AlignmentAuthoring>();
                if (alignment == null) alignment = individualObj.AddComponent<AlignmentAuthoring>();
                alignment.law = individualData.law;
                alignment.good = individualData.good;
                alignment.integrity = individualData.integrity;

                // Add race/culture
                if (individualData.raceId > 0)
                {
                    var raceId = individualObj.GetComponent<RaceIdAuthoring>();
                    if (raceId == null) raceId = individualObj.AddComponent<RaceIdAuthoring>();
                    raceId.raceId = individualData.raceId;
                }

                if (individualData.cultureId > 0)
                {
                    var cultureId = individualObj.GetComponent<CultureIdAuthoring>();
                    if (cultureId == null) cultureId = individualObj.AddComponent<CultureIdAuthoring>();
                    cultureId.cultureId = individualData.cultureId;
                }

                // Add preordain profile
                if (individualData.preordainTrack != PreordainTrack.None)
                {
                    var preordain = individualObj.GetComponent<PreordainProfileAuthoring>();
                    if (preordain == null) preordain = individualObj.AddComponent<PreordainProfileAuthoring>();
                    preordain.track = individualData.preordainTrack;
                }

                // Add titles (individuals can have multiple titles)
                if (individualData.titles != null && individualData.titles.Count > 0)
                {
                    var titles = individualObj.GetComponent<TitlesAuthoring>();
                    if (titles == null) titles = individualObj.AddComponent<TitlesAuthoring>();
                    // Convert catalog title data to authoring title data
                    titles.titles.Clear();
                    foreach (var titleData in individualData.titles)
                    {
                    titles.titles.Add(new TitlesAuthoring.TitleData
                    {
                        tier = titleData.tier,
                        type = titleData.type,
                        level = titleData.level,
                        state = titleData.state,
                        displayName = titleData.displayName,
                        colonyId = titleData.colonyId,
                        factionId = titleData.factionId,
                        empireId = titleData.empireId,
                        acquisitionReason = titleData.acquisitionReason,
                        lossReason = titleData.lossReason
                    });
                    }
                }

                // Add lineage
                if (!string.IsNullOrWhiteSpace(individualData.lineageId))
                {
                    var lineage = individualObj.GetComponent<LineageAuthoring>();
                    if (lineage == null) lineage = individualObj.AddComponent<LineageAuthoring>();
                    lineage.lineageId = individualData.lineageId;
                }

                // Add contract
                if (!string.IsNullOrWhiteSpace(individualData.employerId))
                {
                    var contract = individualObj.GetComponent<ContractAuthoring>();
                    if (contract == null) contract = individualObj.AddComponent<ContractAuthoring>();
                    contract.contractType = individualData.contractType;
                    contract.employerId = individualData.employerId;
                    contract.durationYears = individualData.contractDurationYears;
                }

                // Add loyalty scores
                if (individualData.loyaltyScores != null && individualData.loyaltyScores.Count > 0)
                {
                    var loyaltyScores = individualObj.GetComponent<LoyaltyScoresAuthoring>();
                    if (loyaltyScores == null) loyaltyScores = individualObj.AddComponent<LoyaltyScoresAuthoring>();
                    loyaltyScores.loyaltyScores = individualData.loyaltyScores
                        .Select(ls => new LoyaltyScoresAuthoring.LoyaltyEntry
                        {
                            targetType = ls.targetType,
                            targetId = ls.targetId,
                            loyalty = ls.loyalty
                        })
                        .ToList();
                }

                // Add ownership stakes
                if (individualData.ownershipStakes != null && individualData.ownershipStakes.Count > 0)
                {
                    var ownershipStakes = individualObj.GetComponent<OwnershipStakesAuthoring>();
                    if (ownershipStakes == null) ownershipStakes = individualObj.AddComponent<OwnershipStakesAuthoring>();
                    // Convert catalog entries to authoring entries
                    ownershipStakes.stakes.Clear();
                    foreach (var stake in individualData.ownershipStakes)
                    {
                        ownershipStakes.stakes.Add(new OwnershipStakesAuthoring.StakeEntry
                        {
                            assetType = stake.assetType,
                            assetId = stake.assetId,
                            ownershipPercentage = stake.ownershipPercentage
                        });
                    }
                }

                // Add mentorship
                if (!string.IsNullOrWhiteSpace(individualData.mentorId) || 
                    (individualData.menteeIds != null && individualData.menteeIds.Length > 0))
                {
                    var mentorship = individualObj.GetComponent<MentorshipAuthoring>();
                    if (mentorship == null) mentorship = individualObj.AddComponent<MentorshipAuthoring>();
                    mentorship.mentorId = individualData.mentorId ?? string.Empty;
                    mentorship.menteeIds = individualData.menteeIds ?? new string[0];
                }

                // Add patronage web
                if (individualData.patronages != null && individualData.patronages.Count > 0)
                {
                    var patronageWeb = individualObj.GetComponent<PatronageWebAuthoring>();
                    if (patronageWeb == null) patronageWeb = individualObj.AddComponent<PatronageWebAuthoring>();
                    // Convert catalog entries to authoring entries
                    patronageWeb.patronages.Clear();
                    foreach (var patronage in individualData.patronages)
                    {
                        patronageWeb.patronages.Add(new PatronageWebAuthoring.PatronageEntry
                        {
                            aggregateType = patronage.aggregateType,
                            aggregateId = patronage.aggregateId,
                            role = patronage.role
                        });
                    }
                }

                // Add succession
                if (individualData.successors != null && individualData.successors.Count > 0)
                {
                    var succession = individualObj.GetComponent<SuccessionAuthoring>();
                    if (succession == null) succession = individualObj.AddComponent<SuccessionAuthoring>();
                    // Convert catalog entries to authoring entries
                    succession.successors.Clear();
                    foreach (var successor in individualData.successors)
                    {
                        succession.successors.Add(new SuccessionAuthoring.SuccessorEntry
                        {
                            successorId = successor.successorId,
                            inheritancePercentage = successor.inheritancePercentage,
                            type = successor.type
                        });
                    }
                }

                // Note: Expertise and ServiceTraits are added manually or via runtime systems
                // They can be added to prefabs manually in the editor

                SavePrefab(individualObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.individuals == null) return;

            foreach (var individualData in catalog.individuals)
            {
                string roleFolder = GetRoleFolder(individualData.role);
                var prefabPath = $"{PrefabBasePath}/Individuals/{roleFolder}/{individualData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Individual '{individualData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                }
            }
        }

        private IndividualCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/IndividualCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<IndividualCatalogAuthoring>();
        }

        private string GetRoleFolder(IndividualCatalogAuthoring.IndividualRole role)
        {
            switch (role)
            {
                case IndividualCatalogAuthoring.IndividualRole.Captain:
                case IndividualCatalogAuthoring.IndividualRole.Legend:
                    return "Captains";
                case IndividualCatalogAuthoring.IndividualRole.AceOfficer:
                case IndividualCatalogAuthoring.IndividualRole.JuniorOfficer:
                    return "Officers";
                case IndividualCatalogAuthoring.IndividualRole.CrewSpecialist:
                default:
                    return "Crew";
            }
        }
    }
}

