using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for product templates.
    /// </summary>
    public class ProductEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not ProductTemplate product) return;
            
            DrawCommonFields(product);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Product Properties", EditorStyles.boldLabel);
            
            product.requiredTechTier = (byte)EditorGUILayout.IntSlider("Required Tech Tier", product.requiredTechTier, 0, 255);
            product.presentationArchetype = EditorGUILayout.TextField("Presentation Archetype", product.presentationArchetype);
            
            DrawValidationIssues(product);
        }
    }
}

