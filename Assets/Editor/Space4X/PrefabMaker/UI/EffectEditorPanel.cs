using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for effect templates.
    /// </summary>
    public class EffectEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not EffectTemplate effect) return;
            
            DrawCommonFields(effect);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effect Properties", EditorStyles.boldLabel);
            
            effect.presentationArchetype = EditorGUILayout.TextField("Presentation Archetype", effect.presentationArchetype);
            
            DrawValidationIssues(effect);
        }
    }
}

