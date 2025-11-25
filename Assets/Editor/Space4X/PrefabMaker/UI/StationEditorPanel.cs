using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for station templates.
    /// </summary>
    public class StationEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not StationTemplate station) return;
            
            DrawCommonFields(station);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Station Properties", EditorStyles.boldLabel);
            
            station.hasRefitFacility = EditorGUILayout.Toggle("Has Refit Facility", station.hasRefitFacility);
            if (station.hasRefitFacility)
            {
                station.facilityZoneRadius = EditorGUILayout.FloatField("Facility Zone Radius", station.facilityZoneRadius);
            }
            station.presentationArchetype = EditorGUILayout.TextField("Presentation Archetype", station.presentationArchetype);
            
            DrawValidationIssues(station);
        }
    }
}

