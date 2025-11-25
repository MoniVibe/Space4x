using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for resource templates.
    /// </summary>
    public class ResourceEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not ResourceTemplate resource) return;
            
            DrawCommonFields(resource);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resource Properties", EditorStyles.boldLabel);
            
            resource.presentationArchetype = EditorGUILayout.TextField("Presentation Archetype", resource.presentationArchetype);
            
            DrawValidationIssues(resource);
        }
    }
}

