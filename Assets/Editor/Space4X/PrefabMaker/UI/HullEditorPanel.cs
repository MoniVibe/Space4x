using System.Linq;
using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for hull templates.
    /// </summary>
    public class HullEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not HullTemplate hull) return;
            
            DrawCommonFields(hull);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hull Properties", EditorStyles.boldLabel);
            
            hull.baseMassTons = EditorGUILayout.FloatField("Base Mass (tons)", hull.baseMassTons);
            hull.fieldRefitAllowed = EditorGUILayout.Toggle("Field Refit Allowed", hull.fieldRefitAllowed);
            hull.category = (HullCategory)EditorGUILayout.EnumPopup("Category", hull.category);
            hull.hangarCapacity = EditorGUILayout.FloatField("Hangar Capacity", hull.hangarCapacity);
            hull.presentationArchetype = EditorGUILayout.TextField("Presentation Archetype", hull.presentationArchetype);
            hull.variant = (HullVariant)EditorGUILayout.EnumPopup("Variant", hull.variant);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Properties", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Total Sockets", hull.TotalSocketCount);
            var socketCounts = hull.SocketCountsByType;
            if (socketCounts.Count > 0)
            {
                EditorGUILayout.LabelField("Socket Breakdown:");
                EditorGUI.indentLevel++;
                foreach (var kvp in socketCounts.OrderBy(x => x.Key))
                {
                    EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value}");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sockets", EditorStyles.boldLabel);
            if (hull.slots == null) hull.slots = new System.Collections.Generic.List<HullSlotTemplate>();
            
            EditorGUI.indentLevel++;
            for (int i = 0; i < hull.slots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var slot = hull.slots[i];
                slot.type = (MountType)EditorGUILayout.EnumPopup(slot.type, GUILayout.Width(120));
                slot.size = (MountSize)EditorGUILayout.EnumPopup(slot.size, GUILayout.Width(80));
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    hull.slots.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            
            if (GUILayout.Button("+ Add Socket"))
            {
                hull.slots.Add(new HullSlotTemplate { type = MountType.Weapon, size = MountSize.M });
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Built-in Module Loadouts", EditorStyles.boldLabel);
            if (hull.builtInModuleLoadouts == null) hull.builtInModuleLoadouts = new System.Collections.Generic.List<string>();
            
            EditorGUI.indentLevel++;
            for (int i = 0; i < hull.builtInModuleLoadouts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                hull.builtInModuleLoadouts[i] = EditorGUILayout.TextField(hull.builtInModuleLoadouts[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    hull.builtInModuleLoadouts.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            
            if (GUILayout.Button("+ Add Module Loadout"))
            {
                hull.builtInModuleLoadouts.Add("");
            }
            
            DrawValidationIssues(hull);
        }
    }
}

