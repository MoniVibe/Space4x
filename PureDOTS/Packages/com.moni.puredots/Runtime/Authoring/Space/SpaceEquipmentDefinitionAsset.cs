#if UNITY_EDITOR
using PureDOTS.Runtime.Space;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [CreateAssetMenu(menuName = "PureDOTS/Space/Equipment Definition", fileName = "SpaceEquipmentDefinition")]
    public sealed class SpaceEquipmentDefinitionAsset : ScriptableObject
    {
        public string equipmentId = "equipment.default";
        public string displayName = "Default Equipment";
        public SpaceEquipmentType equipmentType = SpaceEquipmentType.Weapon;
        [Min(0f)] public float mass = 5f;
        [Min(0f)] public float powerDraw = 1f;
        [Min(0f)] public float heatGeneration = 0.5f;
        [TextArea]
        public string description;
    }
}
#endif
