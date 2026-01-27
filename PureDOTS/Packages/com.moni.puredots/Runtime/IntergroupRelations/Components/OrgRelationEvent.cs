using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// Event type affecting organization relations.
    /// </summary>
    public enum OrgRelationEventType : byte
    {
        // Military
        BattleHelp = 0,
        Betrayal = 1,
        Conquest = 2,
        Liberation = 3,
        
        // Economic
        Trade = 4,
        DebtForgiveness = 5,
        Sanctions = 6,
        Embargo = 7,
        
        // Religious/Cultural
        Persecution = 8,
        Tolerance = 9,
        SharedFestival = 10,
        
        // Political
        Coup = 11,
        Assassination = 12,
        DynasticMarriage = 13,
        Vassalization = 14,
        
        // Aid
        HumanitarianAid = 15,
        RefusedAid = 16
    }

    /// <summary>
    /// Event affecting organization relations.
    /// Posted to relation entities to update Attitude/Trust/Fear/Respect/Dependence.
    /// </summary>
    public struct OrgRelationEvent : IComponentData
    {
        public OrgRelationEventType EventType;
        public Entity SourceOrg;
        public Entity TargetOrg;
        
        /// <summary>Attitude change (-100 to +100).</summary>
        public float AttitudeDelta;
        
        /// <summary>Trust change (-1 to +1).</summary>
        public float TrustDelta;
        
        /// <summary>Fear change (-1 to +1).</summary>
        public float FearDelta;
        
        /// <summary>Respect change (-1 to +1).</summary>
        public float RespectDelta;
        
        /// <summary>Dependence change (-1 to +1).</summary>
        public float DependenceDelta;
        
        /// <summary>Treaty flags to toggle.</summary>
        public OrgTreatyFlags TreatyFlagsToAdd;
        public OrgTreatyFlags TreatyFlagsToRemove;
        
        /// <summary>When event occurred.</summary>
        public uint EventTick;
    }
}

