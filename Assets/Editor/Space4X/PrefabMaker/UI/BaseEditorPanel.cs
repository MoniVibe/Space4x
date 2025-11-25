using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Base class for category-specific editor panels.
    /// Provides common UI patterns and validation display.
    /// </summary>
    public abstract class BaseEditorPanel
    {
        protected Vector2 scrollPosition;
        protected string searchFilter = "";
        
        /// <summary>
        /// Draw the editor panel for a template.
        /// </summary>
        public abstract void DrawEditor(PrefabTemplate template);
        
        /// <summary>
        /// Draw validation issues for a template.
        /// </summary>
        protected void DrawValidationIssues(PrefabTemplate template)
        {
            if (template.validationIssues == null || template.validationIssues.Count == 0)
            {
                if (template.isValid)
                {
                    EditorGUILayout.HelpBox("No validation issues.", MessageType.Info);
                }
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation Issues:", EditorStyles.boldLabel);
            foreach (var issue in template.validationIssues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }
        
        /// <summary>
        /// Draw common template fields (ID, display name, description, style tokens).
        /// </summary>
        protected void DrawCommonFields(PrefabTemplate template)
        {
            template.id = EditorGUILayout.TextField("ID", template.id);
            template.displayName = EditorGUILayout.TextField("Display Name", template.displayName);
            template.description = EditorGUILayout.TextArea(template.description ?? "", GUILayout.Height(60));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Style Tokens", EditorStyles.boldLabel);
            template.palette = (byte)EditorGUILayout.IntSlider("Palette", template.palette, 0, 255);
            template.roughness = (byte)EditorGUILayout.IntSlider("Roughness", template.roughness, 0, 255);
            template.pattern = (byte)EditorGUILayout.IntSlider("Pattern", template.pattern, 0, 255);
        }
    }
}

