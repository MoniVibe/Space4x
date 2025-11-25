using System.Linq;
using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for projectile templates.
    /// Projectiles are data-driven (no GameObject logic).
    /// </summary>
    public class ProjectileEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not ProjectileTemplate projectile) return;
            
            EditorGUILayout.HelpBox("Projectiles are data-driven. This editor shows spec data that will be baked to blob assets. Optional presentation tokens (tracers, impacts) can be generated separately.", MessageType.Info);
            
            DrawCommonFields(projectile);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Projectile Properties", EditorStyles.boldLabel);
            
            projectile.kind = (ProjectileKind)EditorGUILayout.EnumPopup("Projectile Kind", projectile.kind);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
            projectile.speed = EditorGUILayout.FloatField("Speed (m/s)", projectile.speed);
            projectile.lifetime = EditorGUILayout.FloatField("Lifetime (s)", projectile.lifetime);
            projectile.gravity = EditorGUILayout.FloatField("Gravity (m/s²)", projectile.gravity);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Homing (for missiles)", EditorStyles.boldLabel);
            projectile.turnRateDeg = EditorGUILayout.FloatField("Turn Rate (deg/s)", projectile.turnRateDeg);
            projectile.seekRadius = EditorGUILayout.FloatField("Seek Radius", projectile.seekRadius);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Penetration & Chaining", EditorStyles.boldLabel);
            projectile.pierce = EditorGUILayout.FloatField("Pierce", projectile.pierce);
            projectile.chainRange = EditorGUILayout.FloatField("Chain Range", projectile.chainRange);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Area of Effect", EditorStyles.boldLabel);
            projectile.aoERadius = EditorGUILayout.FloatField("AoE Radius", projectile.aoERadius);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
            if (projectile.damage == null) projectile.damage = new DamageModelTemplate();
            projectile.damage.kinetic = EditorGUILayout.FloatField("Kinetic", projectile.damage.kinetic);
            projectile.damage.energy = EditorGUILayout.FloatField("Energy", projectile.damage.energy);
            projectile.damage.explosive = EditorGUILayout.FloatField("Explosive", projectile.damage.explosive);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("On Hit Effects", EditorStyles.boldLabel);
            if (projectile.onHitEffects == null) projectile.onHitEffects = new System.Collections.Generic.List<EffectOpTemplate>();
            
            EditorGUI.indentLevel++;
            for (int i = 0; i < projectile.onHitEffects.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var effect = projectile.onHitEffects[i];
                effect.kind = (byte)EditorGUILayout.IntField("Kind", effect.kind);
                effect.magnitude = EditorGUILayout.FloatField("Magnitude", effect.magnitude);
                effect.duration = EditorGUILayout.FloatField("Duration", effect.duration);
                effect.statusId = (uint)EditorGUILayout.IntField("Status ID", (int)effect.statusId);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    projectile.onHitEffects.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            
            if (GUILayout.Button("+ Add On Hit Effect"))
            {
                projectile.onHitEffects.Add(new EffectOpTemplate());
            }
            
            DrawValidationIssues(projectile);
        }
    }
}

