using Unity.Entities;

namespace PureDOTS.Systems.Presentation
{
    /// <summary>
    /// Component storing normalized bar values for HUD rendering.
    /// Updated by UnitBarUpdateSystem, consumed by presentation/UI code.
    /// </summary>
    public struct UnitBarState : IComponentData
    {
        public float Health01;
        public float Mana01;
        public float Stamina01;
        public float Energy01;
        public float Focus01;
        public float ResourceFill01;
        public byte IsSelected; // 0/1
    }
}






















