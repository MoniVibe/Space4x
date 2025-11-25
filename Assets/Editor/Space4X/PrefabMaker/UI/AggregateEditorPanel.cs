using System.Linq;
using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for aggregate templates.
    /// </summary>
    public class AggregateEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not AggregateTemplate aggregate) return;
            
            DrawCommonFields(aggregate);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Aggregate Composition", EditorStyles.boldLabel);
            
            aggregate.templateId = EditorGUILayout.TextField("Template ID", aggregate.templateId);
            aggregate.outlookId = EditorGUILayout.TextField("Outlook ID", aggregate.outlookId);
            aggregate.alignmentId = EditorGUILayout.TextField("Alignment ID", aggregate.alignmentId);
            aggregate.personalityId = EditorGUILayout.TextField("Personality ID", aggregate.personalityId);
            aggregate.themeId = EditorGUILayout.TextField("Theme ID", aggregate.themeId);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Policy Fields", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Policy fields are typically resolved from profile catalogs at runtime. You can override them here if needed.", MessageType.Info);
            
            if (aggregate.policyFields == null) aggregate.policyFields = new System.Collections.Generic.Dictionary<string, float>();
            
            EditorGUI.indentLevel++;
            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (var kvp in aggregate.policyFields.OrderBy(x => x.Key))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(150));
                aggregate.policyFields[kvp.Key] = EditorGUILayout.Slider(aggregate.policyFields[kvp.Key], -1f, 1f);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    keysToRemove.Add(kvp.Key);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            
            foreach (var key in keysToRemove)
            {
                aggregate.policyFields.Remove(key);
            }
            
            if (GUILayout.Button("+ Add Policy Field"))
            {
                aggregate.policyFields["NewField"] = 0f;
            }
            
            DrawValidationIssues(aggregate);
        }
    }
}

