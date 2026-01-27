#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [CreateAssetMenu(menuName = "PureDOTS/Meta Registries/Area Effect Profile", fileName = "AreaEffectProfile")]
    public sealed class AreaEffectProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        public string effectName = "Aura";
        public AreaEffectType effectType = AreaEffectType.Buff;

        [Header("Behaviour")]
        public float currentStrength = 1f;
        public float maxStrength = 1f;
        public float radius = 12f;
        public uint expirationTick;
        public ushort effectId;
        public AreaEffectTargetMask affectedTargets = AreaEffectTargetMask.Villagers | AreaEffectTargetMask.Structures;

        public void CopyTo(AreaEffectAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            authoring.EffectName = effectName;
            authoring.EffectType = effectType;
            authoring.CurrentStrength = currentStrength;
            authoring.MaxStrength = Mathf.Max(currentStrength, maxStrength);
            authoring.Radius = radius;
            authoring.ExpirationTick = expirationTick;
            authoring.EffectId = effectId;
            authoring.AffectedTargets = affectedTargets;
        }
    }
}
#endif


