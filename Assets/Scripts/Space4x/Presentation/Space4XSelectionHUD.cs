using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// MonoBehaviour that displays a HUD panel showing information about the selected entity.
    /// Shows fleet name/id, faction, craft count, cargo load, destination, and current state.
    /// </summary>
    public class Space4XSelectionHUD : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Show selection HUD")]
        public bool ShowHUD = true;

        [Tooltip("HUD position (normalized 0-1, top-right corner)")]
        public Vector2 HUDPosition = new Vector2(0.75f, 0.05f);

        [Tooltip("HUD size")]
        public Vector2 HUDSize = new Vector2(200f, 150f);

        private World _world;
        private EntityQuery _selectionStateQuery;
        private EntityQuery _carrierQuery;
        private EntityQuery _fleetQuery;
        private EntityQuery _movementCommandQuery;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                enabled = false;
                return;
            }

            _selectionStateQuery = _world.EntityManager.CreateEntityQuery(typeof(SelectionState));
            _carrierQuery = _world.EntityManager.CreateEntityQuery(typeof(Carrier));
            _fleetQuery = _world.EntityManager.CreateEntityQuery(typeof(Space4XFleet));
            _movementCommandQuery = _world.EntityManager.CreateEntityQuery(typeof(MovementCommand));
        }

        private void OnGUI()
        {
            if (!ShowHUD || _world == null || !_world.IsCreated)
            {
                return;
            }

            if (_selectionStateQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var selectionState = _selectionStateQuery.GetSingleton<SelectionState>();
            if (selectionState.SelectedCount == 0 || selectionState.PrimarySelected == Entity.Null)
            {
                return;
            }

            // Calculate HUD position (top-right corner)
            float x = Screen.width * HUDPosition.x;
            float y = Screen.height * HUDPosition.y;
            Rect hudRect = new Rect(x, y, HUDSize.x, HUDSize.y);

            // Draw HUD window
            GUILayout.Window(67890, hudRect, DrawSelectionHUD, "Selection Info");
        }

        private void DrawSelectionHUD(int windowID)
        {
            if (_selectionStateQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var selectionState = _selectionStateQuery.GetSingleton<SelectionState>();
            var selectedEntity = selectionState.PrimarySelected;
            var em = _world.EntityManager;

            GUILayout.BeginVertical();

            // Selection type
            GUILayout.Label($"Type: {selectionState.Type}", GUI.skin.box);

            // Fleet/Carrier info
            if (selectionState.Type == SelectionType.Carrier || selectionState.Type == SelectionType.Fleet)
            {
                // Fleet name/id and member count
                if (em.HasComponent<FleetRenderSummary>(selectedEntity))
                {
                    var fleetSummary = em.GetComponentData<FleetRenderSummary>(selectedEntity);
                    GUILayout.Label($"Ships: {fleetSummary.MemberCount}", GUI.skin.label);
                    // Fleet ID from Space4XFleet if available
                    if (em.HasComponent<Space4XFleet>(selectedEntity))
                    {
                        var fleet = em.GetComponentData<Space4XFleet>(selectedEntity);
                        GUILayout.Label($"Fleet ID: {fleet.FleetId}", GUI.skin.label);
                    }
                }
                else if (em.HasComponent<FleetState>(selectedEntity))
                {
                    var fleetState = em.GetComponentData<FleetState>(selectedEntity);
                    GUILayout.Label($"Ships: {fleetState.MemberCount}", GUI.skin.label);
                    if (em.HasComponent<Space4XFleet>(selectedEntity))
                    {
                        var fleet = em.GetComponentData<Space4XFleet>(selectedEntity);
                        GUILayout.Label($"Fleet ID: {fleet.FleetId}", GUI.skin.label);
                    }
                }
                else if (em.HasComponent<Carrier>(selectedEntity))
                {
                    var carrier = em.GetComponentData<Carrier>(selectedEntity);
                    GUILayout.Label($"Carrier", GUI.skin.label);
                }

                // Faction
                if (em.HasComponent<FactionColor>(selectedEntity))
                {
                    var factionColor = em.GetComponentData<FactionColor>(selectedEntity).Value;
                    Color unityColor = new Color(factionColor.x, factionColor.y, factionColor.z, factionColor.w);
                    GUILayout.Label($"Faction: {GetFactionName(unityColor)}", GUI.skin.label);
                }

                // Craft count (for carriers, count crafts with ParentCarrier pointing to this)
                int craftCount = CountCraftsForCarrier(selectedEntity);
                if (craftCount > 0)
                {
                    GUILayout.Label($"Crafts: {craftCount}", GUI.skin.label);
                }

                // Cargo load (if Carrier component has cargo info)
                if (em.HasComponent<Carrier>(selectedEntity))
                {
                    // Would read from Carrier if it had cargo fields
                    // For now, placeholder
                    GUILayout.Label($"Cargo: N/A", GUI.skin.label);
                }

                // Destination (from MovementCommand)
                if (em.HasComponent<MovementCommand>(selectedEntity))
                {
                    var moveCmd = em.GetComponentData<MovementCommand>(selectedEntity);
                    GUILayout.Label($"Destination: ({moveCmd.TargetPosition.x:F1}, {moveCmd.TargetPosition.y:F1})", GUI.skin.label);
                }
                else
                {
                    GUILayout.Label($"Destination: None", GUI.skin.label);
                }

                // Current state
                if (em.HasComponent<CarrierVisualState>(selectedEntity))
                {
                    var visualState = em.GetComponentData<CarrierVisualState>(selectedEntity);
                    GUILayout.Label($"State: {visualState.State}", GUI.skin.label);
                }
            }
            else if (selectionState.Type == SelectionType.Craft)
            {
                // Craft info
                GUILayout.Label($"Craft", GUI.skin.label);

                // Parent carrier
                if (em.HasComponent<ParentCarrier>(selectedEntity))
                {
                    var parentCarrier = em.GetComponentData<ParentCarrier>(selectedEntity).Value;
                    GUILayout.Label($"Parent: Carrier {parentCarrier.Index}", GUI.skin.label);
                }

                // Current state
                if (em.HasComponent<CraftVisualState>(selectedEntity))
                {
                    var visualState = em.GetComponentData<CraftVisualState>(selectedEntity);
                    GUILayout.Label($"State: {visualState.State}", GUI.skin.label);
                }
            }
            else if (selectionState.Type == SelectionType.Asteroid)
            {
                // Asteroid info
                GUILayout.Label($"Asteroid", GUI.skin.label);

                // Resource type
                if (em.HasComponent<ResourceTypeColor>(selectedEntity))
                {
                    var resourceColor = em.GetComponentData<ResourceTypeColor>(selectedEntity).Value;
                    GUILayout.Label($"Resource: {GetResourceTypeName(resourceColor)}", GUI.skin.label);
                }

                // Depletion
                if (em.HasComponent<AsteroidVisualState>(selectedEntity))
                {
                    var visualState = em.GetComponentData<AsteroidVisualState>(selectedEntity);
                    float depletionPercent = visualState.DepletionRatio * 100f;
                    GUILayout.Label($"Depletion: {depletionPercent:F1}%", GUI.skin.label);
                }

                // Resource amount
                if (em.HasComponent<Asteroid>(selectedEntity))
                {
                    var asteroid = em.GetComponentData<Asteroid>(selectedEntity);
                    GUILayout.Label($"Resources: {asteroid.ResourceAmount:F0}/{asteroid.MaxResourceAmount:F0}", GUI.skin.label);
                }
            }

            GUILayout.EndVertical();

            // Make window draggable
            GUI.DragWindow();
        }

        private int CountCraftsForCarrier(Entity carrierEntity)
        {
            int count = 0;
            var craftQuery = _world.EntityManager.CreateEntityQuery(typeof(ParentCarrier), typeof(CraftPresentationTag));
            var parentCarriers = craftQuery.ToComponentDataArray<ParentCarrier>(Unity.Collections.Allocator.Temp);
            
            foreach (var parentCarrier in parentCarriers)
            {
                if (parentCarrier.Value == carrierEntity)
                {
                    count++;
                }
            }
            
            parentCarriers.Dispose();
            return count;
        }

        private string GetFactionName(Color color)
        {
            // Simple color-based faction naming
            if (color.r > 0.7f && color.b < 0.3f && color.g < 0.3f) return "Red";
            if (color.b > 0.7f && color.r < 0.3f && color.g < 0.3f) return "Blue";
            if (color.g > 0.7f && color.r < 0.3f && color.b < 0.3f) return "Green";
            if (color.r > 0.7f && color.g > 0.7f && color.b < 0.3f) return "Yellow";
            return "Unknown";
        }

        private string GetResourceTypeName(float4 color)
        {
            // Simple color-based resource type naming
            if (color.x > 0.5f && color.y > 0.5f && color.z > 0.5f) return "Minerals";
            if (color.y > 0.7f && color.z > 0.7f) return "Rare Metals";
            if (color.z > 0.7f) return "Energy Crystals";
            if (color.y > 0.7f) return "Organic Matter";
            return "Unknown";
        }
    }
}

