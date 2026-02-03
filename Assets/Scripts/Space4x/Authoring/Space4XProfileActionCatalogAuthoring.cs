using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Profile;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Profile Action Catalog")]
    public sealed class Space4XProfileActionCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct ProfileActionDefinitionData
        {
            public ProfileActionToken Token;
            [Header("Alignment Drift (Law/Good/Integrity)")]
            [Range(-1f, 1f)] public float LawDelta;
            [Range(-1f, 1f)] public float GoodDelta;
            [Range(-1f, 1f)] public float IntegrityDelta;
            [Header("Stance Drift (Loyalist/Opportunist/Fanatic/Mutinous)")]
            [Range(-1f, 1f)] public float LoyalistDelta;
            [Range(-1f, 1f)] public float OpportunistDelta;
            [Range(-1f, 1f)] public float FanaticDelta;
            [Range(-1f, 1f)] public float MutinousDelta;
            [Header("Disposition Drift (Compliance/Caution/Formation)")]
            [Range(-1f, 1f)] public float ComplianceDelta;
            [Range(-1f, 1f)] public float CautionDelta;
            [Range(-1f, 1f)] public float FormationAdherenceDelta;
            [Header("Disposition Drift (Risk/Aggression/Patience)")]
            [Range(-1f, 1f)] public float RiskToleranceDelta;
            [Range(-1f, 1f)] public float AggressionDelta;
            [Range(-1f, 1f)] public float PatienceDelta;
            [Header("Weight")]
            [Range(0f, 2f)] public float Weight;

            public ProfileActionDefinitionData Clamp()
            {
                return new ProfileActionDefinitionData
                {
                    Token = Token,
                    LawDelta = math.clamp(LawDelta, -1f, 1f),
                    GoodDelta = math.clamp(GoodDelta, -1f, 1f),
                    IntegrityDelta = math.clamp(IntegrityDelta, -1f, 1f),
                    LoyalistDelta = math.clamp(LoyalistDelta, -1f, 1f),
                    OpportunistDelta = math.clamp(OpportunistDelta, -1f, 1f),
                    FanaticDelta = math.clamp(FanaticDelta, -1f, 1f),
                    MutinousDelta = math.clamp(MutinousDelta, -1f, 1f),
                    ComplianceDelta = math.clamp(ComplianceDelta, -1f, 1f),
                    CautionDelta = math.clamp(CautionDelta, -1f, 1f),
                    FormationAdherenceDelta = math.clamp(FormationAdherenceDelta, -1f, 1f),
                    RiskToleranceDelta = math.clamp(RiskToleranceDelta, -1f, 1f),
                    AggressionDelta = math.clamp(AggressionDelta, -1f, 1f),
                    PatienceDelta = math.clamp(PatienceDelta, -1f, 1f),
                    Weight = math.max(0f, Weight)
                };
            }
        }

        [SerializeField]
        private List<ProfileActionDefinitionData> actions = new List<ProfileActionDefinitionData>();

        private void OnValidate()
        {
            if (actions == null)
            {
                actions = new List<ProfileActionDefinitionData>();
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                actions[i] = actions[i].Clamp();
            }
        }

        private sealed class Baker : Baker<Space4XProfileActionCatalogAuthoring>
        {
            public override void Bake(Space4XProfileActionCatalogAuthoring authoring)
            {
                if (authoring.actions == null || authoring.actions.Count == 0)
                {
                    UnityDebug.LogWarning("[ProfileActionCatalogAuthoring] No profile actions defined.", authoring);
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<ProfileActionCatalogBlob>();
                var array = builder.Allocate(ref catalogBlob.Actions, authoring.actions.Count);

                for (int i = 0; i < authoring.actions.Count; i++)
                {
                    var data = authoring.actions[i].Clamp();
                    array[i] = new ProfileActionDefinition
                    {
                        Token = data.Token,
                        AlignmentDelta = new float3(data.GoodDelta, data.LawDelta, data.IntegrityDelta),
                        StanceDelta = new float4(data.LoyalistDelta, data.OpportunistDelta, data.FanaticDelta, data.MutinousDelta),
                        DispositionDeltaA = new float3(data.ComplianceDelta, data.CautionDelta, data.FormationAdherenceDelta),
                        DispositionDeltaB = new float3(data.RiskToleranceDelta, data.AggressionDelta, data.PatienceDelta),
                        Weight = math.max(0f, data.Weight)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ProfileActionCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProfileActionCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}
