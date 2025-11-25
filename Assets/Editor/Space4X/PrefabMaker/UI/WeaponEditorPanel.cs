using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for weapon templates.
    /// Weapons are data-driven (no GameObject logic).
    /// </summary>
    public class WeaponEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not WeaponTemplate weapon) return;
            
            EditorGUILayout.HelpBox("Weapons are data-driven. This editor shows spec data that will be baked to blob assets. No GameObject prefabs are generated for weapons themselves.", MessageType.Info);
            
            DrawCommonFields(weapon);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Weapon Properties", EditorStyles.boldLabel);
            
            weapon.weaponClass = (WeaponClass)EditorGUILayout.EnumPopup("Weapon Class", weapon.weaponClass);
            weapon.fireRate = EditorGUILayout.FloatField("Fire Rate (shots/sec)", weapon.fireRate);
            weapon.burstCount = (byte)EditorGUILayout.IntSlider("Burst Count", weapon.burstCount, 1, 10);
            weapon.spreadDeg = EditorGUILayout.FloatField("Spread (degrees)", weapon.spreadDeg);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Costs", EditorStyles.boldLabel);
            weapon.energyCost = EditorGUILayout.FloatField("Energy Cost", weapon.energyCost);
            weapon.heatCost = EditorGUILayout.FloatField("Heat Cost", weapon.heatCost);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
            weapon.leadBias = EditorGUILayout.Slider("Lead Bias", weapon.leadBias, 0f, 1f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Projectile", EditorStyles.boldLabel);
            weapon.projectileId = EditorGUILayout.TextField("Projectile ID", weapon.projectileId);
            
            DrawValidationIssues(weapon);
        }
    }
}

