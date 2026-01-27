using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Input
{
    /// <summary>
    /// Single control group (0-9) storing members and optional camera bookmark.
    /// </summary>
    public struct ControlGroup
    {
        public FixedList64Bytes<Entity> Members;
        public bool HasMembers;
        public bool HasCameraBookmark;
        public float3 BookmarkPosition;
        public quaternion BookmarkRotation;
    }

    /// <summary>
    /// Singleton state storing all 10 control groups (indices 0-9).
    /// </summary>
    public struct ControlGroupState : IComponentData
    {
        public FixedList128Bytes<ControlGroup> Groups;  // 10 slots at indices 0-9
    }
}






















