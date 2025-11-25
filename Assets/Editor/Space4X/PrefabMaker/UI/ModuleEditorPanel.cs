using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for module templates.
    /// </summary>
    public class ModuleEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not ModuleTemplate module) return;
            
            DrawCommonFields(module);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Module Properties", EditorStyles.boldLabel);
            
            module.moduleClass = (ModuleClass)EditorGUILayout.EnumPopup("Module Class", module.moduleClass);
            module.requiredMount = (MountType)EditorGUILayout.EnumPopup("Required Mount Type", module.requiredMount);
            module.requiredSize = (MountSize)EditorGUILayout.EnumPopup("Required Size", module.requiredSize);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            module.massTons = EditorGUILayout.FloatField("Mass (tons)", module.massTons);
            module.powerDrawMW = EditorGUILayout.FloatField("Power Draw (MW)", module.powerDrawMW);
            module.offenseRating = (byte)EditorGUILayout.IntSlider("Offense Rating", module.offenseRating, 0, 10);
            module.defenseRating = (byte)EditorGUILayout.IntSlider("Defense Rating", module.defenseRating, 0, 10);
            module.utilityRating = (byte)EditorGUILayout.IntSlider("Utility Rating", module.utilityRating, 0, 10);
            module.defaultEfficiency = EditorGUILayout.Slider("Default Efficiency", module.defaultEfficiency, 0f, 1f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Function", EditorStyles.boldLabel);
            module.function = (ModuleFunction)EditorGUILayout.EnumPopup("Function", module.function);
            if (module.function != ModuleFunction.None)
            {
                module.functionCapacity = EditorGUILayout.FloatField("Function Capacity", module.functionCapacity);
                module.functionDescription = EditorGUILayout.TextArea(module.functionDescription ?? "", GUILayout.Height(40));
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quality/Rarity/Tier/Manufacturer", EditorStyles.boldLabel);
            module.quality = EditorGUILayout.Slider("Quality", module.quality, 0f, 1f);
            module.rarity = (ModuleRarity)EditorGUILayout.EnumPopup("Rarity", module.rarity);
            module.tier = (byte)EditorGUILayout.IntSlider("Tier", module.tier, 0, 255);
            module.manufacturerId = EditorGUILayout.TextField("Manufacturer ID", module.manufacturerId);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Facility Metadata", EditorStyles.boldLabel);
            module.facilityArchetype = (FacilityArchetype)EditorGUILayout.EnumPopup("Facility Archetype", module.facilityArchetype);
            module.facilityTier = (FacilityTier)EditorGUILayout.EnumPopup("Facility Tier", module.facilityTier);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Properties", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Mount Summary", module.MountSummary);
            EditorGUILayout.TextField("Quality Summary", module.QualitySummary);
            EditorGUI.EndDisabledGroup();
            
            DrawValidationIssues(module);
        }
    }
}

