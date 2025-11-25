using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Space4X.Authoring;
using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Editor.PrefabMakerTool.UI;
using Space4X.Editor.PrefabMakerTool.Validation;
using AggregateTemplate = Space4X.Editor.PrefabMakerTool.Models.AggregateTemplate;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Text.RegularExpressions;

namespace Space4X.Editor
{
    public class PrefabMakerWindow : EditorWindow
    {
        private int selectedTab = 0;
        private readonly string[] tabNames = { "Editor", "Batch Generate", "Adopt/Repair", "Validate", "Profiles", "Preview" };
        
        // Editor tab state
        private int selectedCategoryTab = 0;
        private readonly string[] categoryTabs = { "Hulls", "Modules", "Stations", "Resources", "Products", "Aggregates", "FX", "Individuals", "Weapons", "Projectiles", "Turrets" };
        private Vector2 listScrollPosition;
        private Vector2 editorScrollPosition;
        private PrefabTemplate selectedTemplate = null;
        private int selectedTemplateIndex = -1;
        private string editorSearchFilter = "";
        
        // Template storage
        private List<HullTemplate> hullTemplates = new List<HullTemplate>();
        private List<ModuleTemplate> moduleTemplates = new List<ModuleTemplate>();
        private List<StationTemplate> stationTemplates = new List<StationTemplate>();
        private List<ResourceTemplate> resourceTemplates = new List<ResourceTemplate>();
        private List<ProductTemplate> productTemplates = new List<ProductTemplate>();
        private List<AggregateTemplate> aggregateTemplates = new List<AggregateTemplate>();
        private List<EffectTemplate> effectTemplates = new List<EffectTemplate>();
        private List<IndividualTemplate> individualTemplates = new List<IndividualTemplate>();
        private List<WeaponTemplate> weaponTemplates = new List<WeaponTemplate>();
        private List<ProjectileTemplate> projectileTemplates = new List<ProjectileTemplate>();
        private List<TurretTemplate> turretTemplates = new List<TurretTemplate>();
        
        // Editor panels
        private readonly HullEditorPanel hullPanel = new HullEditorPanel();
        private readonly ModuleEditorPanel modulePanel = new ModuleEditorPanel();
        private readonly StationEditorPanel stationPanel = new StationEditorPanel();
        private readonly ResourceEditorPanel resourcePanel = new ResourceEditorPanel();
        private readonly ProductEditorPanel productPanel = new ProductEditorPanel();
        private readonly AggregateEditorPanel aggregatePanel = new AggregateEditorPanel();
        private readonly EffectEditorPanel effectPanel = new EffectEditorPanel();
        private readonly IndividualEditorPanel individualPanel = new IndividualEditorPanel();
        private readonly WeaponEditorPanel weaponPanel = new WeaponEditorPanel();
        private readonly ProjectileEditorPanel projectilePanel = new ProjectileEditorPanel();
        private readonly TurretEditorPanel turretPanel = new TurretEditorPanel();
        
        private string editorCatalogPath = "Assets/Data/Catalogs";

        // Batch Generate tab
        private bool placeholdersOnly = true;
        private bool overwriteMissingSockets = true;
        private bool dryRun = false;
        private string catalogPath = "Assets/Data/Catalogs";
        private Vector2 batchScrollPos;
        private bool[] categoryFilters = new bool[] { true, true, true, true, true }; // CapitalShips, Carriers, Stations, Modules, FX
        private readonly string[] categoryNames = { "Capital Ships", "Carriers", "Stations", "Modules", "FX" };

        // Adopt/Repair tab
        private Vector2 adoptScrollPos;
        private List<string> repairIssues = new List<string>();

        // Validate tab
        private Vector2 validateScrollPos;
        private PrefabMaker.ValidationReport lastReport;

        // Profiles tab
        private Vector2 profilesScrollPos;
        private AggregateComboBuilder.ComboBuildResult lastComboResult;
        private bool profilesDryRun = false;
        private List<uint> selectedAggregateIds = new List<uint>();
        private Vector2 comboListScrollPos;

        [MenuItem("Tools/Space4X/Prefab Maker")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabMakerWindow>("Prefab Maker");
            window.Show();
        }

        private void OnEnable()
        {
            LoadTemplates();
        }
        
        private void OnGUI()
        {
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

            switch (selectedTab)
            {
                case 0:
                    DrawEditorTab();
                    break;
                case 1:
                    DrawBatchGenerateTab();
                    break;
                case 2:
                    DrawAdoptRepairTab();
                    break;
                case 3:
                    DrawValidateTab();
                    break;
                case 4:
                    DrawProfilesTab();
                    break;
                case 5:
                    DrawPreviewSandboxTab();
                    break;
            }
        }
        
        private void LoadTemplates()
        {
            hullTemplates = CatalogTemplateBridge.LoadHullTemplates(editorCatalogPath);
            moduleTemplates = CatalogTemplateBridge.LoadModuleTemplates(editorCatalogPath);
            stationTemplates = CatalogTemplateBridge.LoadStationTemplates(editorCatalogPath);
            resourceTemplates = CatalogTemplateBridge.LoadResourceTemplates(editorCatalogPath);
            productTemplates = CatalogTemplateBridge.LoadProductTemplates(editorCatalogPath);
            aggregateTemplates = CatalogTemplateBridge.LoadAggregateTemplates(editorCatalogPath);
            effectTemplates = CatalogTemplateBridge.LoadEffectTemplates(editorCatalogPath);
            individualTemplates = CatalogTemplateBridge.LoadIndividualTemplates(editorCatalogPath);
            weaponTemplates = CatalogTemplateBridge.LoadWeaponTemplates(editorCatalogPath);
            projectileTemplates = CatalogTemplateBridge.LoadProjectileTemplates(editorCatalogPath);
            turretTemplates = CatalogTemplateBridge.LoadTurretTemplates(editorCatalogPath);
        }
        
        private void DrawEditorTab()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Catalog path
            EditorGUILayout.LabelField("Catalog Path:", GUILayout.Width(100));
            editorCatalogPath = EditorGUILayout.TextField(editorCatalogPath);
            if (GUILayout.Button("Reload", GUILayout.Width(60)))
            {
                LoadTemplates();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Category tabs
            selectedCategoryTab = GUILayout.Toolbar(selectedCategoryTab, categoryTabs);
            
            EditorGUILayout.Space();
            
            // Split view: List on left, Editor on right
            EditorGUILayout.BeginHorizontal();
            
            // Left panel: Template list
            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            
            // Search filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            editorSearchFilter = EditorGUILayout.TextField(editorSearchFilter);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            using (var scroll = new EditorGUILayout.ScrollViewScope(listScrollPosition))
            {
                listScrollPosition = scroll.scrollPosition;
                
                switch (selectedCategoryTab)
                {
                    case 0: DrawHullsList(); break;
                    case 1: DrawModulesList(); break;
                    case 2: DrawStationsList(); break;
                    case 3: DrawResourcesList(); break;
                    case 4: DrawProductsList(); break;
                    case 5: DrawAggregatesList(); break;
                    case 6: DrawEffectsList(); break;
                    case 7: DrawIndividualsList(); break;
                    case 8: DrawWeaponsList(); break;
                    case 9: DrawProjectilesList(); break;
                    case 10: DrawTurretsList(); break;
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // Right panel: Template editor
            EditorGUILayout.BeginVertical();
            
            using (var scroll = new EditorGUILayout.ScrollViewScope(editorScrollPosition))
            {
                editorScrollPosition = scroll.scrollPosition;
                
                if (selectedTemplate != null)
                {
                    DrawTemplateEditor(selectedTemplate);
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a template from the list to edit it.", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Selected Prefab"))
            {
                GenerateSelectedPrefab();
            }
            if (GUILayout.Button("Validate Selected"))
            {
                ValidateSelected();
            }
            if (GUILayout.Button("Generate All Prefabs"))
            {
                GenerateAllPrefabs();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTemplateEditor(PrefabTemplate template)
        {
            switch (template)
            {
                case HullTemplate hull:
                    hullPanel.DrawEditor(hull);
                    break;
                case ModuleTemplate module:
                    modulePanel.DrawEditor(module);
                    break;
                case StationTemplate station:
                    stationPanel.DrawEditor(station);
                    break;
                case ResourceTemplate resource:
                    resourcePanel.DrawEditor(resource);
                    break;
                case ProductTemplate product:
                    productPanel.DrawEditor(product);
                    break;
                case AggregateTemplate aggregate:
                    aggregatePanel.DrawEditor(aggregate);
                    break;
                case EffectTemplate effect:
                    effectPanel.DrawEditor(effect);
                    break;
                case IndividualTemplate individual:
                    individualPanel.DrawEditor(individual);
                    break;
                case WeaponTemplate weapon:
                    weaponPanel.DrawEditor(weapon);
                    break;
                case ProjectileTemplate projectile:
                    projectilePanel.DrawEditor(projectile);
                    break;
                case TurretTemplate turret:
                    turretPanel.DrawEditor(turret);
                    break;
                default:
                    EditorGUILayout.HelpBox("Unknown template type.", MessageType.Warning);
                    break;
            }
        }
        
        private void DrawHullsList()
        {
            EditorGUILayout.LabelField("Hull Templates", EditorStyles.boldLabel);
            var filtered = hullTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                (t.displayName != null && t.displayName.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                    selectedTemplateIndex = i;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                if (!template.isValid && GUILayout.Button("!", GUILayout.Width(20)))
                {
                    // Show validation issues
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawModulesList()
        {
            EditorGUILayout.LabelField("Module Templates", EditorStyles.boldLabel);
            var filtered = moduleTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase) ||
                (t.displayName != null && t.displayName.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                    selectedTemplateIndex = i;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawStationsList()
        {
            EditorGUILayout.LabelField("Station Templates", EditorStyles.boldLabel);
            var filtered = stationTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawResourcesList()
        {
            EditorGUILayout.LabelField("Resource Templates", EditorStyles.boldLabel);
            var filtered = resourceTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawProductsList()
        {
            EditorGUILayout.LabelField("Product Templates", EditorStyles.boldLabel);
            var filtered = productTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawAggregatesList()
        {
            EditorGUILayout.LabelField("Aggregate Templates", EditorStyles.boldLabel);
            var filtered = aggregateTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawEffectsList()
        {
            EditorGUILayout.LabelField("Effect Templates", EditorStyles.boldLabel);
            var filtered = effectTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawIndividualsList()
        {
            EditorGUILayout.LabelField("Individual Templates", EditorStyles.boldLabel);
            var filtered = individualTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawWeaponsList()
        {
            EditorGUILayout.LabelField("Weapon Templates", EditorStyles.boldLabel);
            var filtered = weaponTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawProjectilesList()
        {
            EditorGUILayout.LabelField("Projectile Templates", EditorStyles.boldLabel);
            var filtered = projectileTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawTurretsList()
        {
            EditorGUILayout.LabelField("Turret Templates", EditorStyles.boldLabel);
            var filtered = turretTemplates.Where(t => 
                string.IsNullOrEmpty(editorSearchFilter) || 
                t.id.Contains(editorSearchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            for (int i = 0; i < filtered.Count; i++)
            {
                var template = filtered[i];
                EditorGUILayout.BeginHorizontal("box");
                
                bool isSelected = selectedTemplate == template;
                if (GUILayout.Toggle(isSelected, "", GUILayout.Width(20)))
                {
                    selectedTemplate = template;
                }
                
                EditorGUILayout.LabelField(template.displayName ?? template.id, GUILayout.Width(200));
                EditorGUILayout.LabelField(template.GetSummary(), GUILayout.Width(200));
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void GenerateSelectedPrefab()
        {
            if (selectedTemplate == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a template to generate.", "OK");
                return;
            }
            
            try
            {
                var category = GetCategoryForTemplate(selectedTemplate);
                var result = PrefabMaker.GenerateSelected(
                    editorCatalogPath,
                    new List<string> { selectedTemplate.id },
                    category,
                    placeholdersOnly,
                    overwriteMissingSockets,
                    false
                );
                
                Debug.Log($"Generated prefab for {selectedTemplate.id}. Created: {result.CreatedCount}, Updated: {result.UpdatedCount}");
                if (result.Errors.Count > 0)
                {
                    Debug.LogError($"Generation errors: {string.Join("\n", result.Errors)}");
                }
                if (result.Warnings.Count > 0)
                {
                    Debug.LogWarning($"Generation warnings: {string.Join("\n", result.Warnings)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Prefab generation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private PrefabTemplateCategory? GetCategoryForTemplate(PrefabTemplate template)
        {
            return template switch
            {
                HullTemplate => PrefabTemplateCategory.Hulls,
                ModuleTemplate => PrefabTemplateCategory.Modules,
                StationTemplate => PrefabTemplateCategory.Stations,
                ResourceTemplate => PrefabTemplateCategory.Resources,
                ProductTemplate => PrefabTemplateCategory.Products,
                AggregateTemplate => PrefabTemplateCategory.Aggregates,
                EffectTemplate => PrefabTemplateCategory.FX,
                IndividualTemplate => PrefabTemplateCategory.Individuals,
                WeaponTemplate => PrefabTemplateCategory.Weapons,
                ProjectileTemplate => PrefabTemplateCategory.Projectiles,
                TurretTemplate => PrefabTemplateCategory.Turrets,
                _ => null
            };
        }
        
        private void ValidateSelected()
        {
            if (selectedTemplate == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a template to validate.", "OK");
                return;
            }
            
            TemplateValidator.ValidateTemplate(selectedTemplate);
            
            if (selectedTemplate.isValid)
            {
                EditorUtility.DisplayDialog("Validation", $"Template '{selectedTemplate.id}' is valid.", "OK");
            }
            else
            {
                var issues = string.Join("\n", selectedTemplate.validationIssues);
                EditorUtility.DisplayDialog("Validation Issues", $"Template '{selectedTemplate.id}' has issues:\n\n{issues}", "OK");
            }
        }

        private void DrawBatchGenerateTab()
        {
            GUILayout.Label("Batch Generate Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            catalogPath = EditorGUILayout.TextField("Catalog Path", catalogPath);
            placeholdersOnly = EditorGUILayout.Toggle("Placeholders Only", placeholdersOnly);
            overwriteMissingSockets = EditorGUILayout.Toggle("Overwrite Missing Sockets", overwriteMissingSockets);
            dryRun = EditorGUILayout.Toggle("Dry Run (No Writes)", dryRun);

            EditorGUILayout.Space();
            GUILayout.Label("Category Filters:", EditorStyles.boldLabel);
            for (int i = 0; i < categoryNames.Length; i++)
            {
                categoryFilters[i] = EditorGUILayout.Toggle(categoryNames[i], categoryFilters[i]);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate All Prefabs", GUILayout.Height(30)))
            {
                GenerateAllPrefabs();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Generation Log:", EditorStyles.boldLabel);
            batchScrollPos = EditorGUILayout.BeginScrollView(batchScrollPos, GUILayout.Height(300));
            // Log output would go here
            EditorGUILayout.EndScrollView();
        }

        // Bulk edit filters
        private string bulkEditFilter = "";
        private bool showBulkEditOptions = false;
        private List<string> selectedPrefabPaths = new List<string>();

        private void DrawAdoptRepairTab()
        {
            GUILayout.Label("Adopt/Repair Existing Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Bulk edit section
            showBulkEditOptions = EditorGUILayout.Foldout(showBulkEditOptions, "Bulk Edit & Filters");
            if (showBulkEditOptions)
            {
                EditorGUILayout.BeginHorizontal();
                bulkEditFilter = EditorGUILayout.TextField("Filter by ID/Path:", bulkEditFilter);
                if (GUILayout.Button("Apply Filter", GUILayout.Width(100)))
                {
                    FilterPrefabs();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
                if (GUILayout.Button("Fix All Invalid Fasteners", GUILayout.Height(25)))
                {
                    FixAllInvalidFasteners();
                }
                if (GUILayout.Button("Normalize All IDs", GUILayout.Height(25)))
                {
                    NormalizeAllIds();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan and Repair", GUILayout.Height(30)))
            {
                ScanAndRepair();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Issues Found:", EditorStyles.boldLabel);
            adoptScrollPos = EditorGUILayout.BeginScrollView(adoptScrollPos, GUILayout.Height(300));
            foreach (var issue in repairIssues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private void FilterPrefabs()
        {
            selectedPrefabPaths.Clear();
            var prefabBasePath = "Assets/Prefabs/Space4X";
            var prefabDirs = new[] { "Hulls", "CapitalShips", "Carriers", "Stations", "Modules", "Resources", "Products", "Aggregates", "FX", "Individuals" };
            
            foreach (var dir in prefabDirs)
            {
                var fullDir = $"{prefabBasePath}/{dir}";
                if (!System.IO.Directory.Exists(fullDir)) continue;

                var prefabs = System.IO.Directory.GetFiles(fullDir, "*.prefab", System.IO.SearchOption.AllDirectories);
                foreach (var prefabPath in prefabs)
                {
                    if (string.IsNullOrEmpty(bulkEditFilter) || prefabPath.Contains(bulkEditFilter, System.StringComparison.OrdinalIgnoreCase))
                    {
                        selectedPrefabPaths.Add(prefabPath);
                    }
                }
            }

            Debug.Log($"Filter matched {selectedPrefabPaths.Count} prefabs");
        }

        private void FixAllInvalidFasteners()
        {
            var violations = IdPolicyEnforcer.CheckIdPolicy();
            var fastenerViolations = violations.Where(v => v.ViolationType == "InvalidCase" || v.ViolationType == "InvalidPath").ToList();
            IdPolicyEnforcer.FixViolations(fastenerViolations, false);
            Debug.Log($"Fixed {fastenerViolations.Count} invalid fastener violations");
        }

        private void NormalizeAllIds()
        {
            var violations = IdPolicyEnforcer.CheckIdPolicy();
            var idViolations = violations.Where(v => v.ViolationType == "InvalidCase").ToList();
            IdPolicyEnforcer.FixViolations(idViolations, false);
            Debug.Log($"Normalized {idViolations.Count} IDs");
        }

        private void DrawValidateTab()
        {
            GUILayout.Label("Validate Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Run Validation", GUILayout.Height(30)))
            {
                lastReport = PrefabMaker.ValidateAll();
            }

            if (lastReport != null)
            {
                EditorGUILayout.Space();
                
                // Summary statistics
                var errorCount = lastReport.Issues.Count(i => i.Severity == PrefabMaker.ValidationSeverity.Error);
                var warningCount = lastReport.Issues.Count(i => i.Severity == PrefabMaker.ValidationSeverity.Warning);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Total Issues: {lastReport.TotalIssues}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Errors: {errorCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } });
                EditorGUILayout.LabelField($"Warnings: {warningCount}", new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } });
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Filter options
                EditorGUILayout.LabelField("Filter by Issue Type:", EditorStyles.boldLabel);
                bool showErrors = EditorGUILayout.Toggle("Show Errors", true);
                bool showWarnings = EditorGUILayout.Toggle("Show Warnings", true);
                bool showMissingSockets = EditorGUILayout.Toggle("Show Missing Sockets", true);
                bool showTierMismatches = EditorGUILayout.Toggle("Show Tier Mismatches", true);
                bool showOrphanedPrefabs = EditorGUILayout.Toggle("Show Orphaned Prefabs", true);

                EditorGUILayout.Space();

                // Grouped issues
                var filteredIssues = lastReport.Issues.Where(issue =>
                {
                    if (!showErrors && issue.Severity == PrefabMaker.ValidationSeverity.Error) return false;
                    if (!showWarnings && issue.Severity == PrefabMaker.ValidationSeverity.Warning) return false;
                    if (!showMissingSockets && issue.Message.Contains("socket", StringComparison.OrdinalIgnoreCase)) return false;
                    if (!showTierMismatches && issue.Message.Contains("tier", StringComparison.OrdinalIgnoreCase)) return false;
                    if (!showOrphanedPrefabs && issue.Message.Contains("orphan", StringComparison.OrdinalIgnoreCase)) return false;
                    return true;
                }).ToList();

                // Group by category
                var missingSocketIssues = filteredIssues.Where(i => i.Message.Contains("socket", StringComparison.OrdinalIgnoreCase)).ToList();
                var tierMismatchIssues = filteredIssues.Where(i => i.Message.Contains("tier", StringComparison.OrdinalIgnoreCase)).ToList();
                var orphanedPrefabIssues = filteredIssues.Where(i => i.Message.Contains("orphan", StringComparison.OrdinalIgnoreCase) || i.Message.Contains("no prefab", StringComparison.OrdinalIgnoreCase)).ToList();
                var otherIssues = filteredIssues.Except(missingSocketIssues).Except(tierMismatchIssues).Except(orphanedPrefabIssues).ToList();

                validateScrollPos = EditorGUILayout.BeginScrollView(validateScrollPos, GUILayout.Height(400));

                // Missing sockets section
                if (missingSocketIssues.Count > 0)
                {
                    EditorGUILayout.LabelField($"Missing Sockets ({missingSocketIssues.Count})", EditorStyles.boldLabel);
                    foreach (var issue in missingSocketIssues)
                    {
                        DrawIssueWithQuickLink(issue);
                    }
                    EditorGUILayout.Space();
                }

                // Tier mismatches section
                if (tierMismatchIssues.Count > 0)
                {
                    EditorGUILayout.LabelField($"Tier Mismatches ({tierMismatchIssues.Count})", EditorStyles.boldLabel);
                    foreach (var issue in tierMismatchIssues)
                    {
                        DrawIssueWithQuickLink(issue);
                    }
                    EditorGUILayout.Space();
                }

                // Orphaned prefabs section
                if (orphanedPrefabIssues.Count > 0)
                {
                    EditorGUILayout.LabelField($"Orphaned Prefabs ({orphanedPrefabIssues.Count})", EditorStyles.boldLabel);
                    foreach (var issue in orphanedPrefabIssues)
                    {
                        DrawIssueWithQuickLink(issue);
                    }
                    EditorGUILayout.Space();
                }

                // Other issues section
                if (otherIssues.Count > 0)
                {
                    EditorGUILayout.LabelField($"Other Issues ({otherIssues.Count})", EditorStyles.boldLabel);
                    foreach (var issue in otherIssues)
                    {
                        DrawIssueWithQuickLink(issue);
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Export Report to JSON"))
                {
                    ExportValidationReport();
                }
                if (GUILayout.Button("Select All Error Prefabs"))
                {
                    SelectPrefabsWithIssues(PrefabMaker.ValidationSeverity.Error);
                }
                if (GUILayout.Button("Select All Warning Prefabs"))
                {
                    SelectPrefabsWithIssues(PrefabMaker.ValidationSeverity.Warning);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawIssueWithQuickLink(PrefabMaker.ValidationIssue issue)
        {
            EditorGUILayout.BeginHorizontal();
            var messageType = issue.Severity == PrefabMaker.ValidationSeverity.Error ? MessageType.Error : MessageType.Warning;
            EditorGUILayout.HelpBox($"[{issue.Severity}] {issue.Message}", messageType, true);
            
            if (!string.IsNullOrEmpty(issue.PrefabPath))
            {
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath);
                    if (prefab != null)
                    {
                        Selection.activeObject = prefab;
                        EditorGUIUtility.PingObject(prefab);
                    }
                    else
                    {
                        Debug.LogWarning($"Prefab not found at path: {issue.PrefabPath}");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SelectPrefabsWithIssues(PrefabMaker.ValidationSeverity severity)
        {
            if (lastReport == null) return;

            var prefabs = new List<GameObject>();
            foreach (var issue in lastReport.Issues)
            {
                if (issue.Severity == severity && !string.IsNullOrEmpty(issue.PrefabPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(issue.PrefabPath);
                    if (prefab != null && !prefabs.Contains(prefab))
                    {
                        prefabs.Add(prefab);
                    }
                }
            }

            if (prefabs.Count > 0)
            {
                Selection.objects = prefabs.ToArray();
                EditorGUIUtility.PingObject(prefabs[0]);
                Debug.Log($"Selected {prefabs.Count} prefabs with {severity} issues");
            }
            else
            {
                Debug.Log($"No prefabs found with {severity} issues");
            }
        }

        private void GenerateAllPrefabs()
        {
            try
            {
                var result = PrefabMaker.GenerateAll(catalogPath, placeholdersOnly, overwriteMissingSockets, dryRun);
                Debug.Log($"Prefab generation complete. Created: {result.CreatedCount}, Updated: {result.UpdatedCount}, Skipped: {result.SkippedCount}");
                if (dryRun)
                {
                    Debug.Log("DRY RUN: No files were written.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Prefab generation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ScanAndRepair()
        {
            repairIssues.Clear();
            var result = PrefabAdoptRepair.ScanAndRepair("Assets/Prefabs/Space4X", false);
            repairIssues.Add($"Scanned: {result.ScannedCount}, Repaired: {result.RepairedCount}, Adopted: {result.AdoptedCount}");
            repairIssues.AddRange(result.Issues);
            repairIssues.AddRange(result.Repairs);
        }

        // Preview sandbox
        private bool previewMode = false;
        private string previewSeed = "";
        private Texture2D previewScreenshot;

        private void DrawPreviewSandboxTab()
        {
            GUILayout.Label("Preview Sandbox (No Writes)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            previewMode = EditorGUILayout.Toggle("Enable Preview Mode", previewMode);
            previewSeed = EditorGUILayout.TextField("Seed:", previewSeed);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
            {
                GeneratePreview();
            }

            if (GUILayout.Button("Export Screenshot + JSON Diff", GUILayout.Height(30)))
            {
                ExportPreview();
            }

            if (previewScreenshot != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label("Preview Screenshot:", EditorStyles.boldLabel);
                var rect = EditorGUILayout.GetControlRect(false, 200);
                EditorGUI.DrawPreviewTexture(rect, previewScreenshot);
            }
        }

        private void GeneratePreview()
        {
            // Run generation in dry-run mode
            var options = new PrefabMakerOptions
            {
                CatalogPath = catalogPath,
                PlaceholdersOnly = true,
                OverwriteMissingSockets = false,
                DryRun = true
            };

            var result = PrefabMaker.GenerateAll(options);
            Debug.Log($"Preview generation complete: {result.CreatedCount} would be created, {result.UpdatedCount} would be updated");
        }

        private void ExportPreview()
        {
            // Generate seed
            var seed = ScenarioSeeding.GenerateSeed(catalogPath);
            var seedPath = EditorUtility.SaveFilePanel("Save Preview Seed", "", "preview_seed", "json");
            if (!string.IsNullOrEmpty(seedPath))
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(seed, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(seedPath, json);
                Debug.Log($"Preview seed saved to {seedPath}");
            }
        }

        private void ExportValidationReport()
        {
            if (lastReport == null) return;

            var json = JsonConvert.SerializeObject(lastReport, Formatting.Indented);
            var path = EditorUtility.SaveFilePanel("Save Validation Report", "", "validation_report", "json");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                Debug.Log($"Validation report saved to {path}");
            }
        }

        private void DrawProfilesTab()
        {
            GUILayout.Label("Aggregate Profiles & Combo Table", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            catalogPath = EditorGUILayout.TextField("Catalog Path", catalogPath);
            profilesDryRun = EditorGUILayout.Toggle("Dry Run (No Writes)", profilesDryRun);

            EditorGUILayout.Space();
            GUILayout.Label("Actions:", EditorStyles.boldLabel);

            if (GUILayout.Button("Build Combo Table", GUILayout.Height(30)))
            {
                BuildComboTable();
            }

            EditorGUILayout.Space();

            if (lastComboResult != null)
            {
                GUILayout.Label($"Build Result:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Created Combos:", lastComboResult.CreatedCount.ToString());
                EditorGUILayout.LabelField("Invalid Combos:", lastComboResult.InvalidCount.ToString());
                EditorGUILayout.LabelField("Warnings:", lastComboResult.Warnings.Count.ToString());
                EditorGUILayout.LabelField("Errors:", lastComboResult.Errors.Count.ToString());

                if (lastComboResult.Warnings.Count > 0 || lastComboResult.Errors.Count > 0)
                {
                    EditorGUILayout.Space();
                    GUILayout.Label("Issues:", EditorStyles.boldLabel);
                    profilesScrollPos = EditorGUILayout.BeginScrollView(profilesScrollPos, GUILayout.Height(300));

                    foreach (var error in lastComboResult.Errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }

                    foreach (var warning in lastComboResult.Warnings)
                    {
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    }

                    EditorGUILayout.EndScrollView();
                }

                if (GUILayout.Button("Export Report"))
                {
                    var reportPath = "Assets/Space4X/Reports/space4x_profiles.json";
                    if (File.Exists(reportPath))
                    {
                        EditorUtility.RevealInFinder(reportPath);
                    }
                }

                EditorGUILayout.Space();
                GUILayout.Label("Curated Token Prefabs:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Select aggregate combos to materialize as token prefabs (optional, for showcase/tutorial factions)", MessageType.Info);

                if (lastComboResult?.Combos != null && lastComboResult.Combos.Count > 0)
                {
                    comboListScrollPos = EditorGUILayout.BeginScrollView(comboListScrollPos, GUILayout.Height(200));
                    foreach (var kvp in lastComboResult.Combos)
                    {
                        var isSelected = selectedAggregateIds.Contains(kvp.Key);
                        var newSelected = EditorGUILayout.Toggle(
                            $"{kvp.Value.TemplateId} + {kvp.Value.OutlookId} + {kvp.Value.AlignmentId} ({kvp.Key:X8})",
                            isSelected);
                        
                        if (newSelected && !isSelected)
                        {
                            selectedAggregateIds.Add(kvp.Key);
                        }
                        else if (!newSelected && isSelected)
                        {
                            selectedAggregateIds.Remove(kvp.Key);
                        }
                    }
                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space();
                    if (GUILayout.Button($"Materialize {selectedAggregateIds.Count} Selected Token Prefabs", GUILayout.Height(30)))
                    {
                        MaterializeTokenPrefabs();
                    }
                }
            }
        }

        private void MaterializeTokenPrefabs()
        {
            if (lastComboResult?.Combos == null || selectedAggregateIds.Count == 0)
            {
                Debug.LogWarning("No combos selected for token prefab generation");
                return;
            }

            try
            {
                AggregateTokenPrefabGenerator.MaterializeTokenPrefabs(
                    selectedAggregateIds, 
                    lastComboResult.Combos, 
                    profilesDryRun);
                
                Debug.Log($"Materialized {selectedAggregateIds.Count} token prefabs");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Token prefab generation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void BuildComboTable()
        {
            try
            {
                lastComboResult = AggregateComboBuilder.BuildComboTable(catalogPath, profilesDryRun);
                Debug.Log($"Combo table build complete. Created: {lastComboResult.CreatedCount}, Invalid: {lastComboResult.InvalidCount}");
                if (profilesDryRun)
                {
                    Debug.Log("DRY RUN: No files were written.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Combo table build failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

