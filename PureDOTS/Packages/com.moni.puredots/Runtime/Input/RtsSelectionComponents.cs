using Unity.Entities;

namespace PureDOTS.Input
{
    /// <summary>
    /// Tag component marking an entity as selectable.
    /// </summary>
    public struct SelectableTag : IComponentData { }

    /// <summary>
    /// Tag component marking an entity as currently selected.
    /// </summary>
    public struct SelectedTag : IComponentData { }

    /// <summary>
    /// Component indicating which player/owner controls this entity's selection.
    /// </summary>
    public struct SelectionOwner : IComponentData
    {
        public byte PlayerId;
    }
}






















