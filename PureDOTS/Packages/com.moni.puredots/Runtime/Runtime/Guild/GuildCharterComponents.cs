using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Guild
{
    /// <summary>
    /// Guild charter component for bottom-up guild formation.
    /// </summary>
    public struct GuildCharter : IComponentData
    {
        /// <summary>Entity that founded this charter.</summary>
        public Entity FounderEntity;
        
        /// <summary>Charter fee (wealth requirement).</summary>
        public float CharterFee;
        
        /// <summary>Tick when signature window ends.</summary>
        public uint SignatureWindowEndTick;
        
        /// <summary>Required number of signatures to form guild.</summary>
        public ushort RequiredSignatures;
        
        /// <summary>Proposed guild type ID.</summary>
        public ushort ProposedGuildTypeId;
    }
    
    /// <summary>
    /// Charter signature buffer element.
    /// </summary>
    public struct CharterSignature : IBufferElementData
    {
        /// <summary>Entity that signed the charter.</summary>
        public Entity SignerEntity;
        
        /// <summary>Tick when signature was added.</summary>
        public uint SignedTick;
        
        /// <summary>Motivation for signing (enum: Relations, Alignment, SelfInterest, Fame, NobodyBonus).</summary>
        public byte Motivation;
    }
}
























