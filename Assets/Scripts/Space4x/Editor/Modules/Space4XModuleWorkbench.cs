#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Space4X.Systems.Modules.Bom;
using UnityEditor;
using UnityEngine;

namespace Space4X.EditorTools
{
    public sealed class Space4XModuleWorkbench : EditorWindow
    {
        private const string AnyOption = "<Any>";
        private readonly List<PreviewRow> _rows = new List<PreviewRow>(64);
        private readonly List<string> _families = new List<string>(16);
        private readonly List<string> _manufacturers = new List<string>(16);
        private readonly List<string> _marks = new List<string>(8);
        private readonly HashSet<int> _expanded = new HashSet<int>();
        private readonly Dictionary<string, Space4XModuleFamilyDefinition> _moduleById = new Dictionary<string, Space4XModuleFamilyDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Space4XPartDefinition> _partById = new Dictionary<string, Space4XPartDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Space4XManufacturerDefinition> _manufacturerById = new Dictionary<string, Space4XManufacturerDefinition>(StringComparer.OrdinalIgnoreCase);

        private Space4XModuleBomDeterministicGenerator _generator;
        private string _catalogPath = string.Empty;
        private string _catalogError = string.Empty;

        private uint _seed = 43101u;
        private int _count = 24;
        private float _quality = 0.55f;
        private int _familyIndex;
        private int _manufacturerIndex;
        private int _markIndex;
        private int _selected = -1;

        private uint _comparisonSeed = 53101u;
        private int _comparisonSamples = 50;
        private float _comparisonQuality = 0.55f;
        private int _comparisonFamilyIndex;
        private int _comparisonMarkIndex = 1;
        private int _comparisonAIndex = 1;
        private int _comparisonBIndex = 2;
        private ComparisonSummary _comparison;
        private string _comparisonMessage = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Space4X/Modules/BOM V0/Module Workbench")]
        public static void Open()
        {
            var w = GetWindow<Space4XModuleWorkbench>("Module Workbench");
            w.minSize = new Vector2(920f, 600f);
            w.Show();
        }

        private void OnEnable()
        {
            Reload();
            Generate();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (!string.IsNullOrEmpty(_catalogError))
            {
                EditorGUILayout.HelpBox(_catalogError, MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPreviewControls();
            DrawPreviewList();
            DrawSelected();
            DrawComparison();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Space4X Module Workbench", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Catalog: {Path.GetFileName(_catalogPath)}", EditorStyles.miniLabel);
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    Reload();
                    Generate();
                }
            }
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _seed = DrawUIntField("Seed", _seed);
                _count = EditorGUILayout.IntSlider("Count", _count, 1, 200);
                _quality = EditorGUILayout.Slider("Quality", _quality, 0f, 1f);
                _familyIndex = DrawPopup("Family", _familyIndex, _families);
                _manufacturerIndex = DrawPopup("Manufacturer", _manufacturerIndex, _manufacturers);
                _markIndex = DrawPopup("Mark", _markIndex, _marks);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate", GUILayout.Width(140f)))
                    {
                        Generate();
                    }
                    if (GUILayout.Button("Export CSV", GUILayout.Width(140f)))
                    {
                        ExportPreviewCsv();
                    }
                    if (GUILayout.Button("Export JSON", GUILayout.Width(140f)))
                    {
                        ExportPreviewJson();
                    }
                }
            }
        }

        private void DrawPreviewList()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"Generated Modules ({_rows.Count})", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (var i = 0; i < _rows.Count; i++)
                {
                    var row = _rows[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(i == _selected, i.ToString(CultureInfo.InvariantCulture), "Button", GUILayout.Width(40f)))
                        {
                            _selected = i;
                        }
                        GUILayout.Label(row.Roll.RollId, GUILayout.Width(170f));
                        GUILayout.Label(row.Roll.ManufacturerId, GUILayout.Width(100f));
                        GUILayout.Label(row.Roll.DisplayName, GUILayout.ExpandWidth(true));
                        var isOpen = _expanded.Contains(i);
                        if (GUILayout.Button(isOpen ? "Hide" : "Parts", GUILayout.Width(60f)))
                        {
                            if (isOpen) _expanded.Remove(i); else _expanded.Add(i);
                        }
                    }

                    if (_expanded.Contains(i))
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            var parts = row.Roll.Parts ?? Array.Empty<Space4XModulePartRoll>();
                            for (var p = 0; p < parts.Length; p++)
                            {
                                var part = parts[p];
                                EditorGUILayout.LabelField($"{part.SlotId}[{part.QuantityIndex}] -> {part.Part.PartId} ({part.Part.QualityTier}) q={part.Part.QualityInput:0.000}", EditorStyles.miniLabel);
                            }
                        }
                    }
                }
            }
        }

        private void DrawSelected()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Selected Module", EditorStyles.boldLabel);
            if (_selected < 0 || _selected >= _rows.Count)
            {
                EditorGUILayout.HelpBox("Select a row to inspect stat composition.", MessageType.Info);
                return;
            }

            var row = _rows[_selected];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Name", row.Roll.DisplayName);
                EditorGUILayout.LabelField("RollId", row.Roll.RollId);
                EditorGUILayout.LabelField("Seed", row.Seed.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Family", row.Roll.ModuleFamilyId);
                EditorGUILayout.LabelField("Manufacturer", row.Roll.ManufacturerId);
                EditorGUILayout.LabelField("Mark", row.Roll.Mark.ToString(CultureInfo.InvariantCulture));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Computed Stats", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("DPS", row.Stats.Dps.ToString("0.###", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Heat", row.Stats.Heat.ToString("0.###", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Range", row.Stats.Range.ToString("0.###", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Reliability", row.Stats.Reliability.ToString("0.###", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("PowerDraw", row.Stats.PowerDraw.ToString("0.###", CultureInfo.InvariantCulture));

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Raw Stat Totals", EditorStyles.boldLabel);
                var keys = new List<string>(row.Stats.Raw.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (var i = 0; i < keys.Count; i++)
                {
                    EditorGUILayout.LabelField(keys[i], row.Stats.Raw[keys[i]].ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
        }
        private void DrawComparison()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Comparison Mode", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _comparisonSeed = DrawUIntField("Seed", _comparisonSeed);
                _comparisonSamples = EditorGUILayout.IntSlider("Samples", _comparisonSamples, 5, 200);
                _comparisonQuality = EditorGUILayout.Slider("Quality", _comparisonQuality, 0f, 1f);
                _comparisonFamilyIndex = DrawPopup("Family", _comparisonFamilyIndex, _families);
                _comparisonMarkIndex = DrawPopup("Mark", _comparisonMarkIndex, _marks);
                _comparisonAIndex = DrawPopup("Manufacturer A", _comparisonAIndex, _manufacturers);
                _comparisonBIndex = DrawPopup("Manufacturer B", _comparisonBIndex, _manufacturers);

                if (GUILayout.Button("Run Comparison", GUILayout.Width(150f)))
                {
                    RunComparison();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export Comparison CSV", GUILayout.Width(180f)))
                    {
                        ExportComparisonCsv();
                    }

                    if (GUILayout.Button("Export Comparison MD", GUILayout.Width(180f)))
                    {
                        ExportComparisonMd();
                    }
                }

                if (!string.IsNullOrWhiteSpace(_comparisonMessage))
                {
                    EditorGUILayout.HelpBox(_comparisonMessage, MessageType.None);
                }

                if (_comparison != null)
                {
                    DrawComparisonRow("DPS", _comparison.A.Dps, _comparison.B.Dps);
                    DrawComparisonRow("Heat", _comparison.A.Heat, _comparison.B.Heat);
                    DrawComparisonRow("Range", _comparison.A.Range, _comparison.B.Range);
                    DrawComparisonRow("Reliability", _comparison.A.Reliability, _comparison.B.Reliability);
                    DrawComparisonRow("PowerDraw", _comparison.A.PowerDraw, _comparison.B.PowerDraw);
                    EditorGUILayout.LabelField("Digest", _comparison.Digest.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private static void DrawComparisonRow(string label, float a, float b)
        {
            var delta = a - b;
            EditorGUILayout.LabelField(label, $"A={a:0.###}  B={b:0.###}  delta={delta:+0.###;-0.###;0}");
        }

        private void Reload()
        {
            _rows.Clear();
            _expanded.Clear();
            _selected = -1;
            _comparison = null;
            _comparisonMessage = string.Empty;
            _catalogError = string.Empty;
            _moduleById.Clear();
            _partById.Clear();
            _manufacturerById.Clear();
            _families.Clear();
            _manufacturers.Clear();
            _marks.Clear();

            if (!Space4XModuleBomCatalogV0Loader.TryLoadDefault(out var catalog, out _catalogPath, out var error))
            {
                _catalogError = $"Module catalog load failed: {error}";
                _generator = null;
                return;
            }

            _generator = new Space4XModuleBomDeterministicGenerator(catalog);
            BuildIndexes(catalog);
            BuildFilters(catalog);
        }

        private void BuildIndexes(Space4XModuleBomCatalogV0 catalog)
        {
            var modules = catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>();
            for (var i = 0; i < modules.Length; i++)
            {
                var m = modules[i];
                if (m != null && !string.IsNullOrWhiteSpace(m.id) && !_moduleById.ContainsKey(m.id))
                {
                    _moduleById.Add(m.id, m);
                }
            }

            var parts = catalog.parts ?? Array.Empty<Space4XPartDefinition>();
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p != null && !string.IsNullOrWhiteSpace(p.id) && !_partById.ContainsKey(p.id))
                {
                    _partById.Add(p.id, p);
                }
            }

            var manufacturers = catalog.manufacturers ?? Array.Empty<Space4XManufacturerDefinition>();
            for (var i = 0; i < manufacturers.Length; i++)
            {
                var m = manufacturers[i];
                if (m != null && !string.IsNullOrWhiteSpace(m.id) && !_manufacturerById.ContainsKey(m.id))
                {
                    _manufacturerById.Add(m.id, m);
                }
            }
        }

        private void BuildFilters(Space4XModuleBomCatalogV0 catalog)
        {
            _families.Add(AnyOption);
            _manufacturers.Add(AnyOption);
            _marks.Add(AnyOption);

            var familySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modules = catalog.moduleFamilies ?? Array.Empty<Space4XModuleFamilyDefinition>();
            for (var i = 0; i < modules.Length; i++)
            {
                var id = modules[i]?.id;
                if (!string.IsNullOrWhiteSpace(id)) familySet.Add(id);
            }

            var familyList = new List<string>(familySet);
            familyList.Sort(StringComparer.Ordinal);
            _families.AddRange(familyList);

            var manSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mans = catalog.manufacturers ?? Array.Empty<Space4XManufacturerDefinition>();
            for (var i = 0; i < mans.Length; i++)
            {
                var id = mans[i]?.id;
                if (!string.IsNullOrWhiteSpace(id)) manSet.Add(id);
            }

            var manList = new List<string>(manSet);
            manList.Sort(StringComparer.Ordinal);
            _manufacturers.AddRange(manList);

            var markSet = new HashSet<int>();
            var marks = catalog.marks ?? Array.Empty<Space4XMarkDefinition>();
            for (var i = 0; i < marks.Length; i++)
            {
                var mark = marks[i]?.mark ?? 0;
                if (mark > 0) markSet.Add(mark);
            }

            if (markSet.Count == 0)
            {
                markSet.Add(1);
                markSet.Add(2);
                markSet.Add(3);
            }

            var markList = new List<int>(markSet);
            markList.Sort();
            for (var i = 0; i < markList.Count; i++)
            {
                _marks.Add(markList[i].ToString(CultureInfo.InvariantCulture));
            }

            _familyIndex = Mathf.Clamp(_familyIndex, 0, _families.Count - 1);
            _manufacturerIndex = Mathf.Clamp(_manufacturerIndex, 0, _manufacturers.Count - 1);
            _markIndex = Mathf.Clamp(_markIndex, 0, _marks.Count - 1);
            _comparisonFamilyIndex = Mathf.Clamp(_comparisonFamilyIndex, 0, _families.Count - 1);
            _comparisonMarkIndex = Mathf.Clamp(_comparisonMarkIndex, 0, _marks.Count - 1);
            _comparisonAIndex = Mathf.Clamp(_comparisonAIndex, 0, _manufacturers.Count - 1);
            _comparisonBIndex = Mathf.Clamp(_comparisonBIndex, 0, _manufacturers.Count - 1);
        }

        private void Generate()
        {
            _rows.Clear();
            _expanded.Clear();
            _selected = -1;
            if (_generator == null)
            {
                return;
            }

            var family = ResolveOption(_families, _familyIndex);
            var manufacturer = ResolveOption(_manufacturers, _manufacturerIndex);
            var mark = ResolveMark(_markIndex);

            var familyPool = CollectFamilies(family);
            var markPool = CollectMarks(mark);
            var attempts = 0;
            var maxAttempts = Math.Max(64, _count * 80);
            while (_rows.Count < _count && attempts < maxAttempts)
            {
                var rolledFamily = familyPool[attempts % familyPool.Count];
                var rolledMark = markPool[attempts % markPool.Count];
                var baseSeed = _seed + (uint)(attempts * 7919);

                if (TryRollForManufacturer(baseSeed, rolledFamily, rolledMark, _quality, manufacturer, out var roll, out var usedSeed))
                {
                    _rows.Add(new PreviewRow(usedSeed, roll, ComputeStats(roll)));
                }

                attempts++;
            }

            if (_rows.Count > 0)
            {
                _selected = 0;
            }
        }
        private void RunComparison()
        {
            _comparison = null;
            _comparisonMessage = string.Empty;

            var family = ResolveOption(_families, _comparisonFamilyIndex);
            var manufacturerA = ResolveOption(_manufacturers, _comparisonAIndex);
            var manufacturerB = ResolveOption(_manufacturers, _comparisonBIndex);
            var mark = ResolveMark(_comparisonMarkIndex);

            if (family == AnyOption || manufacturerA == AnyOption || manufacturerB == AnyOption)
            {
                _comparisonMessage = "Choose a specific family and two manufacturers.";
                return;
            }

            if (string.Equals(manufacturerA, manufacturerB, StringComparison.OrdinalIgnoreCase))
            {
                _comparisonMessage = "Manufacturers must be different.";
                return;
            }

            if (mark <= 0)
            {
                _comparisonMessage = "Choose a specific mark.";
                return;
            }

            var aggA = new AggregateStats();
            var aggB = new AggregateStats();
            uint digest = 0x811C9DC5u;

            var produced = 0;
            var attempts = 0;
            var maxAttempts = _comparisonSamples * 40;
            while (produced < _comparisonSamples && attempts < maxAttempts)
            {
                var seed = _comparisonSeed + (uint)(attempts * 15401);
                if (!TryRollForManufacturer(seed, family, mark, _comparisonQuality, manufacturerA, out var rollA, out _))
                {
                    attempts++;
                    continue;
                }
                if (!TryRollForManufacturer(seed, family, mark, _comparisonQuality, manufacturerB, out var rollB, out _))
                {
                    attempts++;
                    continue;
                }

                var statsA = ComputeStats(rollA);
                var statsB = ComputeStats(rollB);
                aggA.Add(statsA);
                aggB.Add(statsB);
                digest = Mix(digest, rollA.Digest);
                digest = Mix(digest, rollB.Digest);
                produced++;
                attempts++;
            }

            if (aggA.Count == 0 || aggB.Count == 0)
            {
                _comparisonMessage = "Unable to generate comparison samples with current filters.";
                return;
            }

            _comparison = new ComparisonSummary(family, mark, manufacturerA, manufacturerB, aggA.Mean(), aggB.Mean(), aggA.Count, aggB.Count, digest);
            _comparisonMessage = $"Generated {_comparison.CountA} samples per manufacturer for {family} Mk{mark}. Digest={digest}.";
        }

        private bool TryRollForManufacturer(uint seed, string family, int mark, float quality, string manufacturerFilter, out Space4XModuleRollResult roll, out uint usedSeed)
        {
            roll = null;
            usedSeed = seed;
            var filter = !string.IsNullOrWhiteSpace(manufacturerFilter) && !string.Equals(manufacturerFilter, AnyOption, StringComparison.Ordinal);
            var attemptSeed = seed;

            for (var i = 0; i < 32; i++)
            {
                if (_generator.RollModule(attemptSeed, family, mark, quality, out var candidate))
                {
                    if (!filter || string.Equals(candidate.ManufacturerId, manufacturerFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        roll = candidate;
                        usedSeed = attemptSeed;
                        return true;
                    }
                }

                attemptSeed = Mix(attemptSeed, (uint)(0x9E3779B9u + (uint)i));
            }

            return false;
        }

        private ModuleStats ComputeStats(Space4XModuleRollResult roll)
        {
            var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (_moduleById.TryGetValue(roll.ModuleFamilyId, out var module))
            {
                AddModuleBase(values, module, roll.Mark);
            }

            var parts = roll.Parts ?? Array.Empty<Space4XModulePartRoll>();
            for (var i = 0; i < parts.Length; i++)
            {
                var partRoll = parts[i].Part;
                if (!_partById.TryGetValue(partRoll.PartId, out var partDef))
                {
                    continue;
                }

                var scalar = ResolveTierScalar(partDef, partRoll.QualityTier);
                var stats = partDef.baseStats ?? Array.Empty<Space4XStatValue>();
                for (var s = 0; s < stats.Length; s++)
                {
                    var key = stats[s]?.key;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    Accumulate(values, key, (stats[s]?.value ?? 0f) * scalar);
                }
            }

            ApplyManufacturer(values, roll.ManufacturerId);
            return BuildModuleStats(values);
        }

        private static ModuleStats BuildModuleStats(Dictionary<string, float> values)
        {
            var damage = Get(values, "damage");
            var penetration = Get(values, "penetration");
            var aoe = Get(values, "aoe");
            var burst = Get(values, "burst_count");
            var cooldown = Get(values, "cooldown");
            var cycle = Mathf.Max(0.05f, Get(values, "cycle_time"));
            var tracking = Get(values, "tracking");
            var stability = Get(values, "stability");
            var durability = Get(values, "durability");
            var efficiency = Mathf.Max(0f, Get(values, "efficiency"));
            var heat = Mathf.Max(0f, Get(values, "heat"));
            var heatDissipation = Mathf.Max(0f, Get(values, "heat_dissipation"));
            var power = Mathf.Max(0f, Get(values, "power"));
            var recoil = Mathf.Max(0f, Get(values, "recoil"));

            var dps = (damage + penetration * 0.6f + aoe * 0.45f + burst * 0.8f) / cycle;
            var heatScore = Mathf.Max(0f, heat + cooldown * 7.5f - heatDissipation * 0.55f);
            var range = 50f + penetration * 1.1f + tracking * 2.2f + stability * 1.3f;
            var reliability = Mathf.Max(0f, stability + durability * 1.05f + efficiency * 9.5f - recoil * 0.35f);
            var powerDraw = power + heat * 0.65f + recoil * 0.45f + burst * 0.5f;
            return new ModuleStats(values, dps, heatScore, range, reliability, powerDraw);
        }

        private static void Accumulate(Dictionary<string, float> values, string key, float delta)
        {
            if (values.TryGetValue(key, out var current))
            {
                values[key] = current + delta;
            }
            else
            {
                values[key] = delta;
            }
        }

        private static float Get(Dictionary<string, float> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value : 0f;
        }

        private static float ResolveTierScalar(Space4XPartDefinition part, string tier)
        {
            var rules = part.qualityTierRules ?? Array.Empty<Space4XQualityTierRule>();
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule != null && string.Equals(rule.tier, tier, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.scalar <= 0f ? 1f : rule.scalar;
                }
            }

            return 1f;
        }

        private void AddModuleBase(Dictionary<string, float> values, Space4XModuleFamilyDefinition module, int mark)
        {
            var bands = module.baseStatsByMark ?? Array.Empty<Space4XMarkStatBand>();
            for (var i = 0; i < bands.Length; i++)
            {
                if (bands[i] == null || bands[i].mark != mark) continue;
                var stats = bands[i].stats ?? Array.Empty<Space4XStatValue>();
                for (var s = 0; s < stats.Length; s++)
                {
                    var key = stats[s]?.key;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        Accumulate(values, key, stats[s]?.value ?? 0f);
                    }
                }
                return;
            }
        }

        private void ApplyManufacturer(Dictionary<string, float> values, string manufacturerId)
        {
            if (!_manufacturerById.TryGetValue(manufacturerId, out var manufacturer))
            {
                return;
            }

            var multipliers = manufacturer.statMultipliers ?? Array.Empty<Space4XStatValue>();
            for (var i = 0; i < multipliers.Length; i++)
            {
                var key = multipliers[i]?.key;
                if (string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out var current))
                {
                    continue;
                }

                values[key] = current * (multipliers[i]?.value ?? 1f);
            }
        }
        private List<string> CollectFamilies(string selected)
        {
            var output = new List<string>(16);
            if (!string.IsNullOrWhiteSpace(selected) && !string.Equals(selected, AnyOption, StringComparison.Ordinal))
            {
                output.Add(selected);
                return output;
            }

            for (var i = 1; i < _families.Count; i++) output.Add(_families[i]);
            return output;
        }

        private List<int> CollectMarks(int selected)
        {
            var output = new List<int>(4);
            if (selected > 0)
            {
                output.Add(selected);
                return output;
            }

            for (var i = 1; i < _marks.Count; i++)
            {
                if (int.TryParse(_marks[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mark)) output.Add(mark);
            }

            return output;
        }

        private void ExportPreviewCsv()
        {
            if (_rows.Count == 0)
            {
                ShowNotification(new GUIContent("Nothing to export."));
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Module Preview CSV", EnsureReportDir(), "space4x_module_workbench_preview", "csv");
            if (string.IsNullOrWhiteSpace(path)) return;

            var sb = new StringBuilder(4096);
            sb.AppendLine("index,seed,rollId,name,family,manufacturer,mark,qualityTarget,digest,dps,heat,range,reliability,powerDraw,parts");
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var parts = BuildPartSummary(row.Roll.Parts);
                sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Seed.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(EscapeCsv(row.Roll.RollId)).Append(',')
                    .Append(EscapeCsv(row.Roll.DisplayName)).Append(',')
                    .Append(EscapeCsv(row.Roll.ModuleFamilyId)).Append(',')
                    .Append(EscapeCsv(row.Roll.ManufacturerId)).Append(',')
                    .Append(row.Roll.Mark.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Roll.QualityTarget.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Roll.Digest.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stats.Dps.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stats.Heat.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stats.Range.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stats.Reliability.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stats.PowerDraw.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(EscapeCsv(parts)).AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Space4XModuleWorkbench] preview CSV exported: {path}");
        }

        private void ExportPreviewJson()
        {
            if (_rows.Count == 0)
            {
                ShowNotification(new GUIContent("Nothing to export."));
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Module Preview JSON", EnsureReportDir(), "space4x_module_workbench_preview", "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            var report = new PreviewExportDoc
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                catalogPath = _catalogPath,
                rows = new PreviewExportRow[_rows.Count]
            };

            for (var i = 0; i < _rows.Count; i++)
            {
                var source = _rows[i];
                var parts = source.Roll.Parts ?? Array.Empty<Space4XModulePartRoll>();
                var partRows = new PreviewExportPart[parts.Length];
                for (var p = 0; p < parts.Length; p++)
                {
                    partRows[p] = new PreviewExportPart
                    {
                        slotId = parts[p].SlotId,
                        quantityIndex = parts[p].QuantityIndex,
                        partId = parts[p].Part.PartId,
                        qualityTier = parts[p].Part.QualityTier,
                        qualityInput = parts[p].Part.QualityInput,
                        rollHash = parts[p].Part.RollHash
                    };
                }

                report.rows[i] = new PreviewExportRow
                {
                    seed = source.Seed,
                    rollId = source.Roll.RollId,
                    displayName = source.Roll.DisplayName,
                    moduleFamilyId = source.Roll.ModuleFamilyId,
                    manufacturerId = source.Roll.ManufacturerId,
                    mark = source.Roll.Mark,
                    qualityTarget = source.Roll.QualityTarget,
                    digest = source.Roll.Digest,
                    dps = source.Stats.Dps,
                    heat = source.Stats.Heat,
                    range = source.Stats.Range,
                    reliability = source.Stats.Reliability,
                    powerDraw = source.Stats.PowerDraw,
                    parts = partRows
                };
            }

            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"[Space4XModuleWorkbench] preview JSON exported: {path}");
        }

        private void ExportComparisonCsv()
        {
            if (_comparison == null)
            {
                ShowNotification(new GUIContent("Run comparison first."));
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Comparison CSV", EnsureReportDir(), "space4x_module_workbench_comparison", "csv");
            if (string.IsNullOrWhiteSpace(path)) return;

            var sb = new StringBuilder(1024);
            sb.AppendLine("metric,manufacturerA,manufacturerB,delta_A_minus_B");
            AppendComparisonCsv(sb, "dps", _comparison.A.Dps, _comparison.B.Dps);
            AppendComparisonCsv(sb, "heat", _comparison.A.Heat, _comparison.B.Heat);
            AppendComparisonCsv(sb, "range", _comparison.A.Range, _comparison.B.Range);
            AppendComparisonCsv(sb, "reliability", _comparison.A.Reliability, _comparison.B.Reliability);
            AppendComparisonCsv(sb, "powerDraw", _comparison.A.PowerDraw, _comparison.B.PowerDraw);
            sb.AppendLine($"digest,{_comparison.Digest},,");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Space4XModuleWorkbench] comparison CSV exported: {path}");
        }

        private void ExportComparisonMd()
        {
            if (_comparison == null)
            {
                ShowNotification(new GUIContent("Run comparison first."));
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Comparison Markdown", EnsureReportDir(), "space4x_module_workbench_comparison", "md");
            if (string.IsNullOrWhiteSpace(path)) return;

            var sb = new StringBuilder(2048);
            sb.AppendLine("# Space4X Module Workbench Comparison");
            sb.AppendLine();
            sb.AppendLine($"- Family: `{_comparison.Family}`");
            sb.AppendLine($"- Mark: `Mk{_comparison.Mark}`");
            sb.AppendLine($"- Manufacturer A: `{_comparison.ManufacturerA}` (samples={_comparison.CountA})");
            sb.AppendLine($"- Manufacturer B: `{_comparison.ManufacturerB}` (samples={_comparison.CountB})");
            sb.AppendLine($"- Digest: `{_comparison.Digest}`");
            sb.AppendLine();
            sb.AppendLine("| Metric | A | B | Delta (A-B) |");
            sb.AppendLine("|---|---:|---:|---:|");
            AppendComparisonMd(sb, "DPS", _comparison.A.Dps, _comparison.B.Dps);
            AppendComparisonMd(sb, "Heat", _comparison.A.Heat, _comparison.B.Heat);
            AppendComparisonMd(sb, "Range", _comparison.A.Range, _comparison.B.Range);
            AppendComparisonMd(sb, "Reliability", _comparison.A.Reliability, _comparison.B.Reliability);
            AppendComparisonMd(sb, "PowerDraw", _comparison.A.PowerDraw, _comparison.B.PowerDraw);
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Space4XModuleWorkbench] comparison markdown exported: {path}");
        }
        private static string EnsureReportDir()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var reports = Path.Combine(root, "Temp", "Reports");
            Directory.CreateDirectory(reports);
            return reports;
        }

        private static void AppendComparisonCsv(StringBuilder sb, string metric, float a, float b)
        {
            var delta = a - b;
            sb.Append(metric).Append(',')
                .Append(a.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(b.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(delta.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)).AppendLine();
        }

        private static void AppendComparisonMd(StringBuilder sb, string metric, float a, float b)
        {
            var delta = a - b;
            sb.Append("| ").Append(metric).Append(" | ")
                .Append(a.ToString("0.###", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(b.ToString("0.###", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(delta.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)).AppendLine(" |");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string BuildPartSummary(Space4XModulePartRoll[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(parts.Length * 16);
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(parts[i].SlotId).Append('=').Append(parts[i].Part.PartId);
            }

            return sb.ToString();
        }

        private static string ResolveOption(List<string> options, int index)
        {
            if (index < 0 || index >= options.Count) return AnyOption;
            return options[index];
        }

        private int ResolveMark(int index)
        {
            if (index <= 0 || index >= _marks.Count) return 0;
            return int.TryParse(_marks[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mark) ? mark : 0;
        }

        private static int DrawPopup(string label, int index, List<string> options)
        {
            if (options.Count == 0) return 0;
            return EditorGUILayout.Popup(label, Mathf.Clamp(index, 0, options.Count - 1), options.ToArray());
        }

        private static uint DrawUIntField(string label, uint value)
        {
            var input = EditorGUILayout.LongField(label, value);
            if (input < 0) return 0u;
            if (input > uint.MaxValue) return uint.MaxValue;
            return (uint)input;
        }

        private static uint Mix(uint seed, uint value)
        {
            unchecked
            {
                var hash = seed ^ 2166136261u;
                hash ^= value;
                hash *= 16777619u;
                hash ^= hash >> 13;
                hash *= 16777619u;
                return hash;
            }
        }

        private readonly struct PreviewRow
        {
            public PreviewRow(uint seed, Space4XModuleRollResult roll, ModuleStats stats)
            {
                Seed = seed;
                Roll = roll;
                Stats = stats;
            }

            public uint Seed { get; }
            public Space4XModuleRollResult Roll { get; }
            public ModuleStats Stats { get; }
        }

        private sealed class ModuleStats
        {
            public ModuleStats(Dictionary<string, float> raw, float dps, float heat, float range, float reliability, float powerDraw)
            {
                Raw = raw;
                Dps = dps;
                Heat = heat;
                Range = range;
                Reliability = reliability;
                PowerDraw = powerDraw;
            }

            public Dictionary<string, float> Raw { get; }
            public float Dps { get; }
            public float Heat { get; }
            public float Range { get; }
            public float Reliability { get; }
            public float PowerDraw { get; }
        }

        private sealed class AggregateStats
        {
            public int Count;
            public float Dps;
            public float Heat;
            public float Range;
            public float Reliability;
            public float PowerDraw;

            public void Add(ModuleStats stats)
            {
                Count++;
                Dps += stats.Dps;
                Heat += stats.Heat;
                Range += stats.Range;
                Reliability += stats.Reliability;
                PowerDraw += stats.PowerDraw;
            }

            public AvgStats Mean()
            {
                if (Count == 0) return default;
                var inv = 1f / Count;
                return new AvgStats(Dps * inv, Heat * inv, Range * inv, Reliability * inv, PowerDraw * inv);
            }
        }

        private readonly struct AvgStats
        {
            public AvgStats(float dps, float heat, float range, float reliability, float powerDraw)
            {
                Dps = dps;
                Heat = heat;
                Range = range;
                Reliability = reliability;
                PowerDraw = powerDraw;
            }

            public float Dps { get; }
            public float Heat { get; }
            public float Range { get; }
            public float Reliability { get; }
            public float PowerDraw { get; }
        }

        private sealed class ComparisonSummary
        {
            public ComparisonSummary(string family, int mark, string manufacturerA, string manufacturerB, AvgStats a, AvgStats b, int countA, int countB, uint digest)
            {
                Family = family;
                Mark = mark;
                ManufacturerA = manufacturerA;
                ManufacturerB = manufacturerB;
                A = a;
                B = b;
                CountA = countA;
                CountB = countB;
                Digest = digest;
            }

            public string Family { get; }
            public int Mark { get; }
            public string ManufacturerA { get; }
            public string ManufacturerB { get; }
            public AvgStats A { get; }
            public AvgStats B { get; }
            public int CountA { get; }
            public int CountB { get; }
            public uint Digest { get; }
        }

        [Serializable]
        private sealed class PreviewExportDoc
        {
            public string generatedAtUtc;
            public string catalogPath;
            public PreviewExportRow[] rows;
        }

        [Serializable]
        private sealed class PreviewExportRow
        {
            public uint seed;
            public string rollId;
            public string displayName;
            public string moduleFamilyId;
            public string manufacturerId;
            public int mark;
            public float qualityTarget;
            public uint digest;
            public float dps;
            public float heat;
            public float range;
            public float reliability;
            public float powerDraw;
            public PreviewExportPart[] parts;
        }

        [Serializable]
        private sealed class PreviewExportPart
        {
            public string slotId;
            public int quantityIndex;
            public string partId;
            public string qualityTier;
            public float qualityInput;
            public uint rollHash;
        }
    }
}
#endif
