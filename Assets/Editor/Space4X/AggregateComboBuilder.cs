using System;
using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    public static class AggregateComboBuilder
    {
        public class ComboBuildResult
        {
            public int CreatedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int SkippedCount { get; set; }
            public int InvalidCount { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> Errors { get; set; } = new List<string>();
            public Dictionary<uint, ComposedAggregateSpec> Combos { get; set; } = new Dictionary<uint, ComposedAggregateSpec>();
        }

        public class ComboValidationIssue
        {
            public string ComboKey { get; set; }
            public string Reason { get; set; }
            public ValidationSeverity Severity { get; set; }
        }

        public enum ValidationSeverity
        {
            Warning,
            Error
        }

        public static ComboBuildResult BuildComboTable(string catalogPath, bool dryRun)
        {
            var result = new ComboBuildResult();

            try
            {
                // Load all profile catalogs
                var templateCatalog = LoadTemplateCatalog(catalogPath);
                var outlookCatalog = LoadOutlookCatalog(catalogPath);
                var alignmentCatalog = LoadAlignmentCatalog(catalogPath);
                var personalityCatalog = LoadPersonalityCatalog(catalogPath);
                var themeCatalog = LoadThemeCatalog(catalogPath);

                if (templateCatalog == null || outlookCatalog == null || alignmentCatalog == null || 
                    personalityCatalog == null || themeCatalog == null)
                {
                    result.Errors.Add("Failed to load one or more profile catalogs");
                    return result;
                }

                // Generate all combinations
                var combos = new List<ComposedAggregateSpec>();
                var validationIssues = new List<ComboValidationIssue>();

                foreach (var template in templateCatalog.templates)
                {
                    foreach (var outlook in outlookCatalog.profiles)
                    {
                        foreach (var alignment in alignmentCatalog.profiles)
                        {
                            foreach (var personality in personalityCatalog.archetypes)
                            {
                                foreach (var theme in themeCatalog.profiles)
                                {
                                    var combo = ComposeAggregate(
                                        template, outlook, alignment, personality, theme, 
                                        validationIssues);

                                    if (combo.HasValue)
                                    {
                                        combos.Add(combo.Value);
                        result.Combos[combo.Value.AggregateId32] = combo.Value;
                                    }
                                    else
                                    {
                                        result.InvalidCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                // Report validation issues
                foreach (var issue in validationIssues)
                {
                    if (issue.Severity == ValidationSeverity.Error)
                    {
                        result.Errors.Add($"{issue.ComboKey}: {issue.Reason}");
                    }
                    else
                    {
                        result.Warnings.Add($"{issue.ComboKey}: {issue.Reason}");
                    }
                }

                if (dryRun)
                {
                    result.CreatedCount = combos.Count;
                    return result;
                }

                // Build blob asset
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var comboTableBlob = ref builder.ConstructRoot<AggregateComboTableBlob>();
                var comboArray = builder.Allocate(ref comboTableBlob.Combos, combos.Count);

                for (int i = 0; i < combos.Count; i++)
                {
                    comboArray[i] = combos[i];
                }

                var blobAsset = builder.CreateBlobAssetReference<AggregateComboTableBlob>(Allocator.Persistent);
                builder.Dispose();

                // Save blob asset (would need ScriptableObject wrapper or direct blob asset file)
                // For now, save as JSON report
                SaveComboTableReport(combos, catalogPath, result);

                result.CreatedCount = combos.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Exception during combo table build: {ex.Message}");
                Debug.LogError($"AggregateComboBuilder exception: {ex}\n{ex.StackTrace}");
            }

            return result;
        }

        private static ComposedAggregateSpec? ComposeAggregate(
            AggregateTemplateCatalogAuthoring.AggregateTemplateData template,
            OutlookProfileCatalogAuthoring.OutlookProfileData outlook,
            AlignmentProfileCatalogAuthoring.AlignmentProfileData alignment,
            PersonalityArchetypeCatalogAuthoring.PersonalityArchetypeData personality,
            ThemeProfileCatalogAuthoring.ThemeProfileData theme,
            List<ComboValidationIssue> validationIssues)
        {
            var comboKey = $"{template.id}+{outlook.id}+{alignment.id}+{personality.id}+{theme.id}";

            // Validate tech gates
            if (template.techCap < template.techFloor)
            {
                validationIssues.Add(new ComboValidationIssue
                {
                    ComboKey = comboKey,
                    Reason = $"TechCap ({template.techCap}) < TechFloor ({template.techFloor})",
                    Severity = ValidationSeverity.Error
                });
                return null;
            }

            // Start from template defaults
            var spec = new ComposedAggregateSpec
            {
                TemplateId = new FixedString32Bytes(template.id),
                OutlookId = new FixedString32Bytes(outlook.id),
                AlignmentId = new FixedString32Bytes(alignment.id),
                PersonalityId = new FixedString32Bytes(personality.id),
                ThemeId = new FixedString32Bytes(theme.id),
                TechFloor = template.techFloor,
                TechCap = template.techCap,
                CrewGradeMean = template.crewGradeMean,
                LogisticsTolerance = template.logisticsTolerance
            };

            // Start from template defaults (base values)
            float baseAggression = 0f;
            float baseTradeBias = 0f;
            float baseDiplomacy = 0f;
            float baseDoctrineMissile = 0.33f;
            float baseDoctrineLaser = 0.33f;
            float baseDoctrineHangar = 0.34f;
            float baseFieldRefitMult = 1f;

            // Apply Outlook weights (0.6)
            spec.Aggression = baseAggression + (outlook.aggression * 0.6f);
            spec.TradeBias = baseTradeBias + (outlook.tradeBias * 0.6f);
            spec.Diplomacy = baseDiplomacy + (outlook.diplomacy * 0.6f);
            spec.DoctrineMissile = baseDoctrineMissile + (outlook.doctrineMissile - 0.33f) * 0.6f;
            spec.DoctrineLaser = baseDoctrineLaser + (outlook.doctrineLaser - 0.33f) * 0.6f;
            spec.DoctrineHangar = baseDoctrineHangar + (outlook.doctrineHangar - 0.34f) * 0.6f;
            spec.FieldRefitMult = baseFieldRefitMult + (outlook.fieldRefitMult - 1f) * 0.6f;

            // Apply Alignment weights (0.4)
            spec.Ethics = alignment.ethics * 0.4f;
            spec.Order = alignment.order * 0.4f;
            spec.CollateralLimit = alignment.collateralLimit * 0.4f;
            spec.PiracyTolerance = alignment.piracyTolerance * 0.4f;
            spec.DiplomacyBias = alignment.diplomacyBias * 0.4f;

            // Modulate by Personality (±20%)
            var personalityMod = 1f + (personality.risk * 0.2f);
            spec.Aggression *= personalityMod;
            spec.Risk = personality.risk;
            spec.Opportunism = personality.opportunism;
            spec.Caution = personality.caution;
            spec.Zeal = personality.zeal;
            spec.CooldownMult = personality.cooldownMult;

            // Normalize doctrine weights
            var doctrineSum = spec.DoctrineMissile + spec.DoctrineLaser + spec.DoctrineHangar;
            if (doctrineSum > 0.001f)
            {
                spec.DoctrineMissile /= doctrineSum;
                spec.DoctrineLaser /= doctrineSum;
                spec.DoctrineHangar /= doctrineSum;
            }
            else
            {
                // Fallback to equal distribution
                spec.DoctrineMissile = 0.33f;
                spec.DoctrineLaser = 0.33f;
                spec.DoctrineHangar = 0.34f;
            }

            // Clamp values
            spec.Aggression = math.clamp(spec.Aggression, -1f, 1f);
            spec.TradeBias = math.clamp(spec.TradeBias, -1f, 1f);
            spec.Diplomacy = math.clamp(spec.Diplomacy, -1f, 1f);
            spec.FieldRefitMult = math.clamp(spec.FieldRefitMult, 0.5f, 2f);
            spec.CollateralLimit = math.clamp(spec.CollateralLimit, 0f, 1f);
            spec.PiracyTolerance = math.clamp(spec.PiracyTolerance, 0f, 1f);
            spec.CooldownMult = math.clamp(spec.CooldownMult, 0.5f, 2f);

            // Apply style tokens from theme
            spec.StyleTokens = new StyleTokens
            {
                Palette = theme.palette,
                Roughness = 128, // Default
                Pattern = theme.pattern
            };

            // Generate deterministic hash
            spec.AggregateId32 = ComputeAggregateHash(
                template.id, outlook.id, alignment.id, personality.id, theme.id);

            return spec;
        }

        public static uint ComputeAggregateHash(
            string templateId, string outlookId, string alignmentId, 
            string personalityId, string themeId)
        {
            // Deterministic hash combining all IDs
            var combined = $"{templateId}|{outlookId}|{alignmentId}|{personalityId}|{themeId}";
            return (uint)combined.GetHashCode();
        }

        private static void SaveComboTableReport(
            List<ComposedAggregateSpec> combos, string catalogPath, ComboBuildResult result)
        {
            try
            {
                var reportDir = "Assets/Space4X/Reports";
                if (!AssetDatabase.IsValidFolder(reportDir))
                {
                    AssetDatabase.CreateFolder("Assets/Space4X", "Reports");
                }

                var reportPath = $"{reportDir}/space4x_profiles.json";
                var report = new
                {
                    Version = "1.0",
                    GeneratedAt = DateTime.UtcNow.ToString("O"),
                    ComboCount = combos.Count,
                    Combos = combos.Select(c => new
                    {
                        AggregateId32 = c.AggregateId32,
                        TemplateId = c.TemplateId.ToString(),
                        OutlookId = c.OutlookId.ToString(),
                        AlignmentId = c.AlignmentId.ToString(),
                        PersonalityId = c.PersonalityId.ToString(),
                        ThemeId = c.ThemeId.ToString(),
                        Policy = new
                        {
                            Aggression = c.Aggression,
                            TradeBias = c.TradeBias,
                            Diplomacy = c.Diplomacy,
                            DoctrineMissile = c.DoctrineMissile,
                            DoctrineLaser = c.DoctrineLaser,
                            DoctrineHangar = c.DoctrineHangar,
                            FieldRefitMult = c.FieldRefitMult,
                            Ethics = c.Ethics,
                            Order = c.Order,
                            CollateralLimit = c.CollateralLimit,
                            PiracyTolerance = c.PiracyTolerance,
                            Risk = c.Risk,
                            Opportunism = c.Opportunism,
                            Caution = c.Caution,
                            Zeal = c.Zeal,
                            CooldownMult = c.CooldownMult,
                            TechFloor = c.TechFloor,
                            TechCap = c.TechCap
                        }
                    }).ToArray(),
                    Warnings = result.Warnings,
                    Errors = result.Errors
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(reportPath, json);
                AssetDatabase.ImportAsset(reportPath);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to save combo table report: {ex.Message}");
            }
        }

        private static AggregateTemplateCatalogAuthoring LoadTemplateCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/AggregateTemplateCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<AggregateTemplateCatalogAuthoring>();
        }

        private static OutlookProfileCatalogAuthoring LoadOutlookCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/OutlookProfileCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<OutlookProfileCatalogAuthoring>();
        }

        private static AlignmentProfileCatalogAuthoring LoadAlignmentCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/AlignmentProfileCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<AlignmentProfileCatalogAuthoring>();
        }

        private static PersonalityArchetypeCatalogAuthoring LoadPersonalityCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/PersonalityArchetypeCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<PersonalityArchetypeCatalogAuthoring>();
        }

        private static ThemeProfileCatalogAuthoring LoadThemeCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/ThemeProfileCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<ThemeProfileCatalogAuthoring>();
        }

        public static List<ComboValidationIssue> ValidateProfiles(string catalogPath)
        {
            var issues = new List<ComboValidationIssue>();

            try
            {
                var templateCatalog = LoadTemplateCatalog(catalogPath);
                var outlookCatalog = LoadOutlookCatalog(catalogPath);
                var alignmentCatalog = LoadAlignmentCatalog(catalogPath);
                var personalityCatalog = LoadPersonalityCatalog(catalogPath);
                var themeCatalog = LoadThemeCatalog(catalogPath);

                if (templateCatalog == null || outlookCatalog == null || alignmentCatalog == null ||
                    personalityCatalog == null || themeCatalog == null)
                {
                    issues.Add(new ComboValidationIssue
                    {
                        ComboKey = "Catalog Loading",
                        Reason = "Failed to load one or more profile catalogs",
                        Severity = ValidationSeverity.Error
                    });
                    return issues;
                }

                // Validate template hull percentages sum to 100
                foreach (var template in templateCatalog.templates)
                {
                    var sum = template.hullLightPct + template.hullCarrierPct + template.hullHeavyPct;
                    if (sum != 100)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Template:{template.id}",
                            Reason = $"Hull percentages sum to {sum}, expected 100",
                            Severity = ValidationSeverity.Warning
                        });
                    }

                    // Validate tech gates
                    if (template.techCap < template.techFloor)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Template:{template.id}",
                            Reason = $"TechCap ({template.techCap}) < TechFloor ({template.techFloor})",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }

                // Validate outlook doctrine weights normalize
                foreach (var outlook in outlookCatalog.profiles)
                {
                    var doctrineSum = outlook.doctrineMissile + outlook.doctrineLaser + outlook.doctrineHangar;
                    if (Math.Abs(doctrineSum - 1f) > 0.01f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Outlook:{outlook.id}",
                            Reason = $"Doctrine weights sum to {doctrineSum}, expected 1.0",
                            Severity = ValidationSeverity.Warning
                        });
                    }

                    // Validate field refit multiplier range
                    if (outlook.fieldRefitMult < 0.5f || outlook.fieldRefitMult > 2f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Outlook:{outlook.id}",
                            Reason = $"FieldRefitMult ({outlook.fieldRefitMult}) out of range [0.5, 2.0]",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }

                // Validate alignment policy limits
                foreach (var alignment in alignmentCatalog.profiles)
                {
                    if (alignment.collateralLimit < 0f || alignment.collateralLimit > 1f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Alignment:{alignment.id}",
                            Reason = $"CollateralLimit ({alignment.collateralLimit}) out of range [0, 1]",
                            Severity = ValidationSeverity.Error
                        });
                    }

                    if (alignment.piracyTolerance < 0f || alignment.piracyTolerance > 1f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Alignment:{alignment.id}",
                            Reason = $"PiracyTolerance ({alignment.piracyTolerance}) out of range [0, 1]",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }

                // Validate personality cooldown multiplier
                foreach (var personality in personalityCatalog.archetypes)
                {
                    if (personality.cooldownMult < 0.5f || personality.cooldownMult > 2f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Personality:{personality.id}",
                            Reason = $"CooldownMult ({personality.cooldownMult}) out of range [0.5, 2.0]",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }

                // Build combo table and validate normalization
                var buildResult = BuildComboTable(catalogPath, true);
                foreach (var combo in buildResult.Combos.Values)
                {
                    var doctrineSum = combo.DoctrineMissile + combo.DoctrineLaser + combo.DoctrineHangar;
                    if (Math.Abs(doctrineSum - 1f) > 0.01f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Combo:{combo.AggregateId32}",
                            Reason = $"Normalized doctrine weights sum to {doctrineSum}, expected 1.0",
                            Severity = ValidationSeverity.Error
                        });
                    }

                    // Validate policy sanity
                    if (combo.CollateralLimit < 0f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Combo:{combo.AggregateId32}",
                            Reason = $"CollateralLimit ({combo.CollateralLimit}) < 0",
                            Severity = ValidationSeverity.Error
                        });
                    }

                    if (combo.FieldRefitMult < 0.5f || combo.FieldRefitMult > 2f)
                    {
                        issues.Add(new ComboValidationIssue
                        {
                            ComboKey = $"Combo:{combo.AggregateId32}",
                            Reason = $"FieldRefitMult ({combo.FieldRefitMult}) out of range [0.5, 2.0]",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ComboValidationIssue
                {
                    ComboKey = "Validation",
                    Reason = $"Exception during validation: {ex.Message}",
                    Severity = ValidationSeverity.Error
                });
            }

            return issues;
        }
    }
}

