using System.Linq;
using Space4X.Editor.PrefabMakerTool.Models;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.PrefabMakerTool.UI
{
    /// <summary>
    /// Editor panel for individual entity templates.
    /// </summary>
    public class IndividualEditorPanel : BaseEditorPanel
    {
        public override void DrawEditor(PrefabTemplate template)
        {
            if (template is not IndividualTemplate individual) return;
            
            DrawCommonFields(individual);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Role & Progression", EditorStyles.boldLabel);
            
            individual.role = (IndividualRole)EditorGUILayout.EnumPopup("Role", individual.role);
            individual.preordainTrack = (Space4X.Registry.PreordainTrack)EditorGUILayout.EnumPopup("Preordain Track", individual.preordainTrack);
            individual.lineageId = EditorGUILayout.TextField("Lineage ID", individual.lineageId);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Core Stats", EditorStyles.boldLabel);
            
            individual.command = EditorGUILayout.Slider("Command", individual.command, 0f, 100f);
            individual.tactics = EditorGUILayout.Slider("Tactics", individual.tactics, 0f, 100f);
            individual.logistics = EditorGUILayout.Slider("Logistics", individual.logistics, 0f, 100f);
            individual.diplomacy = EditorGUILayout.Slider("Diplomacy", individual.diplomacy, 0f, 100f);
            individual.engineering = EditorGUILayout.Slider("Engineering", individual.engineering, 0f, 100f);
            individual.resolve = EditorGUILayout.Slider("Resolve", individual.resolve, 0f, 100f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physique/Finesse/Will", EditorStyles.boldLabel);
            
            individual.physique = EditorGUILayout.Slider("Physique", individual.physique, 0f, 100f);
            individual.finesse = EditorGUILayout.Slider("Finesse", individual.finesse, 0f, 100f);
            individual.will = EditorGUILayout.Slider("Will", individual.will, 0f, 100f);
            individual.physiqueInclination = EditorGUILayout.Slider("Physique Inclination", individual.physiqueInclination, 1f, 10f);
            individual.finesseInclination = EditorGUILayout.Slider("Finesse Inclination", individual.finesseInclination, 1f, 10f);
            individual.willInclination = EditorGUILayout.Slider("Will Inclination", individual.willInclination, 1f, 10f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Alignment", EditorStyles.boldLabel);
            
            individual.law = EditorGUILayout.Slider("Law", individual.law, -1f, 1f);
            individual.good = EditorGUILayout.Slider("Good", individual.good, -1f, 1f);
            individual.integrity = EditorGUILayout.Slider("Integrity", individual.integrity, -1f, 1f);
            individual.raceId = (ushort)EditorGUILayout.IntField("Race ID", individual.raceId);
            individual.cultureId = (ushort)EditorGUILayout.IntField("Culture ID", individual.cultureId);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Contract", EditorStyles.boldLabel);
            
            individual.contractType = (Space4X.Registry.ContractType)EditorGUILayout.EnumPopup("Contract Type", individual.contractType);
            individual.employerId = EditorGUILayout.TextField("Employer ID", individual.employerId);
            individual.contractDurationYears = EditorGUILayout.Slider("Duration (years)", individual.contractDurationYears, 1f, 5f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Properties", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Stats Summary", individual.StatsSummary);
            EditorGUI.EndDisabledGroup();
            
            // Titles, loyalty scores, etc. can be expanded in a foldout if needed
            EditorGUILayout.Space();
            if (EditorGUILayout.Foldout(false, "Advanced Relations (Titles, Loyalty, Patronage, etc.)"))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("These relations are typically managed at runtime. Edit in catalog for initial values.", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            
            DrawValidationIssues(individual);
        }
    }
}

