using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Input
{
    /// <summary>
    /// Authoring component to mark baked entities as selectable and optionally assign an owner id.
    /// </summary>
    public sealed class SelectableAuthoring : MonoBehaviour
    {
        [Tooltip("Optional player/owner id. Leave -1 to skip adding SelectionOwner.")]
        public int ownerId = -1;

    }
}
