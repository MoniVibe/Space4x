using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Culture Dire Tactics Policy Catalog")]
    public sealed class Space4XCultureDireTacticsPolicyAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct CulturePolicyEntry
        {
            public ushort cultureId;
            public bool allowKamikaze;
            public bool allowExtremeOrders;
        }

        public List<CulturePolicyEntry> entries = new List<CulturePolicyEntry>();

        public sealed class Baker : Baker<Space4XCultureDireTacticsPolicyAuthoring>
        {
            public override void Bake(Space4XCultureDireTacticsPolicyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<CultureDireTacticsPolicyCatalog>(entity);
                var buffer = AddBuffer<CultureDireTacticsPolicy>(entity);
                foreach (var entry in authoring.entries)
                {
                    buffer.Add(new CultureDireTacticsPolicy
                    {
                        CultureId = entry.cultureId,
                        AllowKamikaze = (byte)(entry.allowKamikaze ? 1 : 0),
                        AllowExtremeOrders = (byte)(entry.allowExtremeOrders ? 1 : 0)
                    });
                }
            }
        }
    }
}
