using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for turret templates.
    /// Turrets are data-driven (no GameObject logic).
    /// </summary>
    public class TurretEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not TurretTemplate turret) return;
            
            EditorGUILayout.HelpBox("Turrets are data-driven. This editor shows spec data that will be baked to blob assets. Optional presentation tokens (turret shells with sockets) can be generated separately.", MessageType.Info);
            
            DrawCommonFields(turret);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Turret Properties", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Traverse", EditorStyles.boldLabel);
            turret.arcLimitDeg = EditorGUILayout.Slider("Arc Limit (deg)", turret.arcLimitDeg, 0f, 360f);
            turret.traverseSpeedDegPerSec = EditorGUILayout.FloatField("Traverse Speed (deg/s)", turret.traverseSpeedDegPerSec);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Elevation", EditorStyles.boldLabel);
            turret.elevationMinDeg = EditorGUILayout.Slider("Min Elevation (deg)", turret.elevationMinDeg, -90f, 90f);
            turret.elevationMaxDeg = EditorGUILayout.Slider("Max Elevation (deg)", turret.elevationMaxDeg, -90f, 90f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Recoil", EditorStyles.boldLabel);
            turret.recoilForce = EditorGUILayout.FloatField("Recoil Force", turret.recoilForce);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Socket", EditorStyles.boldLabel);
            turret.socketName = EditorGUILayout.TextField("Socket Name", turret.socketName);
            
            DrawValidationIssues(turret);
        }
    }
}

