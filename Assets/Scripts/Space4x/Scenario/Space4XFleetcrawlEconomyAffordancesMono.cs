using System.Collections.Generic;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4x.Scenario
{
    [DisallowMultipleComponent]
    public sealed class Space4XFleetcrawlEconomyAffordancesMono : MonoBehaviour
    {
        private enum OrbKind : byte
        {
            Credits = 0,
            Materials = 1
        }

        private sealed class OrbView
        {
            public GameObject Root;
            public Renderer BodyRenderer;
            public Transform RingTransform;
            public OrbKind Kind;
            public int Amount;
            public int RoomIndex;
            public float Phase;
            public float3 Position;
        }

        private sealed class ShopNodeView
        {
            public GameObject Root;
            public Renderer BodyRenderer;
            public Renderer BeaconRenderer;
            public Transform BeaconTransform;
            public Space4XFleetcrawlPurchaseKind Kind;
            public FixedString64Bytes PurchaseId;
            public int Ordinal;
            public float3 Position;
        }

        private struct ShopOption
        {
            public Space4XFleetcrawlPurchaseKind Kind;
            public FixedString64Bytes PurchaseId;
        }

        [SerializeField] private float enemyOrbOffsetMin = 1.8f;
        [SerializeField] private float enemyOrbOffsetMax = 4.3f;
        [SerializeField] private float rewardOrbOffsetMin = 3.6f;
        [SerializeField] private float rewardOrbOffsetMax = 7.2f;
        [SerializeField] private float orbCollectionRadius = 6f;
        [SerializeField] private float orbPulseHz = 1.1f;
        [SerializeField] private float orbPulseAmplitude = 0.17f;
        [SerializeField] private float shopForwardDistance = 34f;
        [SerializeField] private float shopLateralSpread = 13f;
        [SerializeField] private float shopMarkerHeight = 1.7f;
        [SerializeField] private float shopMarkerLerp = 8f;
        [SerializeField] private float shopProximityRadius = 10f;
        [SerializeField] private float shopPulseHz = 0.65f;
        [SerializeField] private float shopPulseAmplitude = 0.12f;
        [SerializeField] private int maxOrbs = 40;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _scenarioQuery;
        private EntityQuery _directorQuery;
        private EntityQuery _flagshipQuery;
        private EntityQuery _destroyedEnemyQuery;
        private bool _queriesReady;

        private readonly List<OrbView> _orbs = new List<OrbView>(32);
        private readonly HashSet<Entity> _destroyedEnemySeen = new HashSet<Entity>();
        private readonly ShopNodeView[] _shopNodes = new ShopNodeView[3];

        private int _activeRoomIndex = -1;
        private int _lastCurrencyValue = int.MinValue;
        private int _lastPurchaseRoom = int.MinValue;
        private int _lastPurchaseOrdinal = int.MinValue;
        private float3 _shopAnchorPosition;
        private float3 _shopAnchorForward = new float3(0f, 0f, 1f);
        private bool _loggedReady;

        private void OnDisable()
        {
            SetAllVisualsVisible(false);
        }

        private void OnDestroy()
        {
            DestroyOrbVisuals();
            DestroyShopVisuals();
        }

        private void Update()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
            {
                SetAllVisualsVisible(false);
                return;
            }

            if (!TryEnsureQueries())
            {
                return;
            }

            if (_scenarioQuery.IsEmptyIgnoreFilter || _directorQuery.IsEmptyIgnoreFilter || _flagshipQuery.IsEmptyIgnoreFilter)
            {
                SetShopVisible(false);
                return;
            }

            var scenario = _scenarioQuery.GetSingleton<ScenarioInfo>();
            if (!Space4XFleetcrawlUiBridge.IsFleetcrawlScenario(scenario.ScenarioId))
            {
                SetAllVisualsVisible(false);
                return;
            }

            var directorEntity = _directorQuery.GetSingletonEntity();
            var director = _entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            if (director.RunCompleted != 0 || director.CurrentRoomIndex < 0)
            {
                SetShopVisible(false);
                UpdateOrbs(float3.zero, playerValid: false);
                return;
            }

            var rooms = _entityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
            if (rooms.Length == 0)
            {
                SetShopVisible(false);
                return;
            }

            var roomIndex = math.clamp(director.CurrentRoomIndex, 0, rooms.Length - 1);
            var room = rooms[roomIndex];
            var flagshipEntity = _flagshipQuery.GetSingletonEntity();
            var flagshipTransform = _entityManager.GetComponentData<LocalTransform>(flagshipEntity);
            var flagshipPosition = flagshipTransform.Position;
            var flagshipForward = math.normalizesafe(math.mul(flagshipTransform.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));

            if (roomIndex != _activeRoomIndex)
            {
                _activeRoomIndex = roomIndex;
                _destroyedEnemySeen.Clear();
                _shopAnchorPosition = flagshipPosition;
                _shopAnchorForward = flagshipForward;
                _lastPurchaseRoom = int.MinValue;
                _lastPurchaseOrdinal = int.MinValue;
                if (_entityManager.HasComponent<Space4XRunPendingPurchaseRequest>(directorEntity))
                {
                    var stale = _entityManager.GetComponentData<Space4XRunPendingPurchaseRequest>(directorEntity);
                    if (stale.RoomIndex != roomIndex)
                    {
                        _entityManager.RemoveComponent<Space4XRunPendingPurchaseRequest>(directorEntity);
                    }
                }
            }
            else
            {
                _shopAnchorPosition = math.lerp(_shopAnchorPosition, flagshipPosition, math.saturate(0.68f * Time.deltaTime));
                _shopAnchorForward = math.normalizesafe(math.lerp(_shopAnchorForward, flagshipForward, math.saturate(0.82f * Time.deltaTime)), _shopAnchorForward);
            }

            SpawnEnemyDropOrbs(director.Seed);
            SpawnRewardOrbs(directorEntity, director.Seed, roomIndex, flagshipPosition);

            var reliefGateSelected = IsReliefGateSelected(directorEntity, roomIndex, room.Kind);
            var showShop = room.Kind == Space4XFleetcrawlRoomKind.Relief || reliefGateSelected;
            if (showShop)
            {
                EnsureShopNodesCreated();
                UpdateShopNodes(roomIndex, room.Kind, reliefGateSelected);
                ResolveShopPurchaseRequest(directorEntity, roomIndex, room.Kind, flagshipPosition, reliefGateSelected);
            }
            else
            {
                SetShopVisible(false);
            }

            UpdateOrbs(flagshipPosition, playerValid: true);

            if (!_loggedReady)
            {
                _loggedReady = true;
                Debug.Log("[FleetcrawlEconomy] ready=1 visuals=orbs+shop");
            }
        }

        private void SpawnEnemyDropOrbs(uint seed)
        {
            if (_destroyedEnemyQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = _destroyedEnemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _destroyedEnemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var tags = _destroyedEnemyQuery.ToComponentDataArray<Space4XRunEnemyTag>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (_destroyedEnemySeen.Contains(entity))
                {
                    continue;
                }

                _destroyedEnemySeen.Add(entity);
                var enemyTag = tags[i];
                var eventPosition = transforms[i].Position;
                var key = HashEnemyDrop(seed, enemyTag.RoomIndex, enemyTag.WaveIndex, eventPosition);
                var spawnPosition = eventPosition + ComputeDeterministicOffset(key, enemyOrbOffsetMin, enemyOrbOffsetMax);
                SpawnOrb(OrbKind.Materials, spawnPosition + new float3(0f, 1.4f, 0f), enemyTag.RoomIndex, amount: 1, key);
            }
        }

        private void SpawnRewardOrbs(Entity directorEntity, uint seed, int roomIndex, float3 flagshipPosition)
        {
            if (!_entityManager.HasComponent<RunCurrency>(directorEntity))
            {
                return;
            }

            var currency = _entityManager.GetComponentData<RunCurrency>(directorEntity).Value;
            if (_lastCurrencyValue == int.MinValue)
            {
                _lastCurrencyValue = currency;
                return;
            }

            if (currency <= _lastCurrencyValue)
            {
                _lastCurrencyValue = currency;
                return;
            }

            var delta = currency - _lastCurrencyValue;
            _lastCurrencyValue = currency;
            var count = math.clamp((delta + 39) / 40, 1, 3);
            var amountPerOrb = math.max(1, delta / count);
            for (var i = 0; i < count; i++)
            {
                var key = HashRewardDrop(seed, roomIndex, delta, i);
                var offset = ComputeDeterministicOffset(key, rewardOrbOffsetMin, rewardOrbOffsetMax);
                SpawnOrb(OrbKind.Credits, flagshipPosition + offset + new float3(0f, 1.8f, 0f), roomIndex, amountPerOrb, key);
            }
        }

        private void SpawnOrb(OrbKind kind, float3 position, int roomIndex, int amount, uint key)
        {
            if (_orbs.Count >= maxOrbs)
            {
                Destroy(_orbs[0].Root);
                _orbs.RemoveAt(0);
            }

            var root = new GameObject($"EconomyOrb_{kind}_{roomIndex}_{key:X8}");
            root.transform.SetParent(transform, worldPositionStays: false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, worldPositionStays: false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.82f, 0.82f, 0.82f);
            var bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(root.transform, worldPositionStays: false);
            ring.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            ring.transform.localScale = new Vector3(0.98f, 0.03f, 0.98f);
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null)
            {
                Destroy(ringCollider);
            }

            var bodyRenderer = body.GetComponent<Renderer>();
            var ringRenderer = ring.GetComponent<Renderer>();
            var color = kind == OrbKind.Credits
                ? new Color(1f, 0.82f, 0.16f, 1f)
                : new Color(0.2f, 0.9f, 1f, 1f);
            ApplyColor(bodyRenderer, color, emissionMultiplier: 0.34f);
            ApplyColor(ringRenderer, color * 0.92f, emissionMultiplier: 0.2f);

            _orbs.Add(new OrbView
            {
                Root = root,
                BodyRenderer = bodyRenderer,
                RingTransform = ring.transform,
                Kind = kind,
                Amount = amount,
                RoomIndex = roomIndex,
                Phase = ((key & 0xFFu) / 255f) * math.PI * 2f,
                Position = position
            });
        }

        private void UpdateOrbs(float3 flagshipPosition, bool playerValid)
        {
            for (var i = _orbs.Count - 1; i >= 0; i--)
            {
                var orb = _orbs[i];
                if (orb.Root == null)
                {
                    _orbs.RemoveAt(i);
                    continue;
                }

                var pulse = 1f + Mathf.Sin((Time.unscaledTime * orbPulseHz * Mathf.PI * 2f) + orb.Phase) * orbPulseAmplitude;
                var scale = Mathf.Max(0.2f, pulse);
                orb.Root.transform.localScale = new Vector3(scale, scale, scale);
                if (orb.RingTransform != null)
                {
                    orb.RingTransform.Rotate(0f, 115f * Time.deltaTime, 0f, Space.Self);
                }

                orb.Position = orb.Root.transform.position;
                if (!playerValid)
                {
                    continue;
                }

                var distanceSq = math.lengthsq(flagshipPosition - orb.Position);
                if (distanceSq > orbCollectionRadius * orbCollectionRadius)
                {
                    continue;
                }

                PlayOrbCollectionFeedback(orb);
                _orbs.RemoveAt(i);
            }
        }

        private void PlayOrbCollectionFeedback(OrbView orb)
        {
            if (orb.BodyRenderer != null)
            {
                ApplyColor(orb.BodyRenderer, Color.white, emissionMultiplier: 0.4f);
            }

            if (orb.Root != null)
            {
                orb.Root.transform.localScale *= 1.45f;
                Destroy(orb.Root, 0.09f);
            }

            var label = orb.Kind == OrbKind.Credits ? "credits" : "materials";
            Debug.Log($"[FleetcrawlEconomy] PICKUP room={orb.RoomIndex} type={label} amount={orb.Amount} via=proximity");
        }

        private bool IsReliefGateSelected(Entity directorEntity, int roomIndex, Space4XFleetcrawlRoomKind currentRoomKind)
        {
            if (!_entityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity))
            {
                return false;
            }

            var pending = _entityManager.GetComponentData<Space4XRunPendingGatePick>(directorEntity);
            if (pending.RoomIndex != roomIndex)
            {
                return false;
            }

            var gateKind = Space4XFleetcrawlUiBridge.ResolveGateKind(currentRoomKind, math.clamp(pending.GateOrdinal, 0, 2));
            return gateKind == Space4XRunGateKind.Relief;
        }

        private void EnsureShopNodesCreated()
        {
            for (var i = 0; i < _shopNodes.Length; i++)
            {
                if (_shopNodes[i] != null && _shopNodes[i].Root != null)
                {
                    continue;
                }

                var root = new GameObject($"ShopNode_{i}");
                root.transform.SetParent(transform, worldPositionStays: false);

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, worldPositionStays: false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(0.8f, 1.15f, 0.8f);
                var bodyCollider = body.GetComponent<Collider>();
                if (bodyCollider != null)
                {
                    Destroy(bodyCollider);
                }

                var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "BeaconRing";
                beacon.transform.SetParent(root.transform, worldPositionStays: false);
                beacon.transform.localPosition = new Vector3(0f, 1.12f, 0f);
                beacon.transform.localScale = new Vector3(1.2f, 0.035f, 1.2f);
                var beaconCollider = beacon.GetComponent<Collider>();
                if (beaconCollider != null)
                {
                    Destroy(beaconCollider);
                }

                _shopNodes[i] = new ShopNodeView
                {
                    Root = root,
                    BodyRenderer = body.GetComponent<Renderer>(),
                    BeaconRenderer = beacon.GetComponent<Renderer>(),
                    BeaconTransform = beacon.transform,
                    Ordinal = i,
                    Kind = (Space4XFleetcrawlPurchaseKind)255,
                    PurchaseId = default
                };
            }
        }

        private void UpdateShopNodes(int roomIndex, Space4XFleetcrawlRoomKind roomKind, bool reliefGateSelected)
        {
            var right = math.normalizesafe(new float3(_shopAnchorForward.z, 0f, -_shopAnchorForward.x), new float3(1f, 0f, 0f));
            var anchor = _shopAnchorPosition + _shopAnchorForward * shopForwardDistance + new float3(0f, shopMarkerHeight, 0f);
            for (var ordinal = 0; ordinal < _shopNodes.Length; ordinal++)
            {
                var node = _shopNodes[ordinal];
                node.Root.SetActive(true);

                var option = ResolveShopOption(roomKind, reliefGateSelected, ordinal);
                var kindChanged = node.Kind != option.Kind || !node.PurchaseId.Equals(option.PurchaseId);
                node.Kind = option.Kind;
                node.PurchaseId = option.PurchaseId;

                var lane = ordinal switch
                {
                    0 => -1f,
                    1 => 0f,
                    _ => 1f
                };

                var targetPos = anchor + right * (lane * shopLateralSpread);
                node.Root.transform.position = Vector3.Lerp(node.Root.transform.position, targetPos, Mathf.Clamp01(shopMarkerLerp * Time.deltaTime));
                node.Root.transform.localScale = new Vector3(1.22f, 1.22f, 1.22f);
                node.Root.name = $"ShopNode_{ordinal}_{option.Kind}_R{roomIndex}";
                node.Position = node.Root.transform.position;

                if (kindChanged)
                {
                    ApplyShopNodeColor(node, option.Kind);
                }

                if (node.BeaconTransform != null)
                {
                    var pulse = 1f + Mathf.Sin((Time.unscaledTime * shopPulseHz * Mathf.PI * 2f) + (ordinal * 0.9f)) * shopPulseAmplitude;
                    var scale = Mathf.Max(0.3f, pulse);
                    node.BeaconTransform.localScale = new Vector3(1.2f * scale, 0.035f, 1.2f * scale);
                }
            }
        }

        private void ResolveShopPurchaseRequest(Entity directorEntity, int roomIndex, Space4XFleetcrawlRoomKind roomKind, float3 flagshipPosition, bool reliefGateSelected)
        {
            var nearestOrdinal = -1;
            var nearestDistanceSq = float.MaxValue;
            var radiusSq = shopProximityRadius * shopProximityRadius;
            for (var i = 0; i < _shopNodes.Length; i++)
            {
                var node = _shopNodes[i];
                if (node?.Root == null || !node.Root.activeSelf)
                {
                    continue;
                }

                var distanceSq = math.lengthsq(flagshipPosition - node.Position);
                if (distanceSq > radiusSq || distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                nearestOrdinal = i;
            }

            if (nearestOrdinal < 0)
            {
                return;
            }

            var option = ResolveShopOption(roomKind, reliefGateSelected, nearestOrdinal);
            var request = new Space4XRunPendingPurchaseRequest
            {
                RoomIndex = roomIndex,
                NodeOrdinal = nearestOrdinal,
                Kind = option.Kind,
                PurchaseId = option.PurchaseId
            };

            var hasExisting = _entityManager.HasComponent<Space4XRunPendingPurchaseRequest>(directorEntity);
            var changed = true;
            if (hasExisting)
            {
                var existing = _entityManager.GetComponentData<Space4XRunPendingPurchaseRequest>(directorEntity);
                changed = existing.RoomIndex != request.RoomIndex ||
                          existing.NodeOrdinal != request.NodeOrdinal ||
                          existing.Kind != request.Kind ||
                          !existing.PurchaseId.Equals(request.PurchaseId);
            }

            if (!changed)
            {
                return;
            }

            if (hasExisting)
            {
                _entityManager.SetComponentData(directorEntity, request);
            }
            else
            {
                _entityManager.AddComponentData(directorEntity, request);
            }

            if (_lastPurchaseRoom != roomIndex || _lastPurchaseOrdinal != nearestOrdinal)
            {
                _lastPurchaseRoom = roomIndex;
                _lastPurchaseOrdinal = nearestOrdinal;
                Debug.Log($"[FleetcrawlShop] REQUEST room={roomIndex} option={nearestOrdinal} purchase={option.PurchaseId} kind={Space4XFleetcrawlUiBridge.DescribePurchase(option.Kind)} via=proximity");
            }
        }

        private static ShopOption ResolveShopOption(Space4XFleetcrawlRoomKind roomKind, bool reliefGateSelected, int ordinal)
        {
            if (roomKind == Space4XFleetcrawlRoomKind.Relief)
            {
                return ordinal switch
                {
                    0 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.DamageBoost, PurchaseId = new FixedString64Bytes("shop_damage_boost_small") },
                    1 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.CooldownTrim, PurchaseId = new FixedString64Bytes("shop_cooldown_trim_small") },
                    _ => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.Heal, PurchaseId = new FixedString64Bytes("shop_hull_patch_small") }
                };
            }

            if (reliefGateSelected)
            {
                return ordinal switch
                {
                    0 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.Heal, PurchaseId = new FixedString64Bytes("shop_hull_patch_small") },
                    1 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.RerollToken, PurchaseId = new FixedString64Bytes("shop_reroll_token") },
                    _ => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.DamageBoost, PurchaseId = new FixedString64Bytes("shop_damage_boost_small") }
                };
            }

            return ordinal switch
            {
                0 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.DamageBoost, PurchaseId = new FixedString64Bytes("shop_damage_boost_small") },
                1 => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.CooldownTrim, PurchaseId = new FixedString64Bytes("shop_cooldown_trim_small") },
                _ => new ShopOption { Kind = Space4XFleetcrawlPurchaseKind.Heal, PurchaseId = new FixedString64Bytes("shop_hull_patch_small") }
            };
        }

        private static void ApplyShopNodeColor(ShopNodeView node, Space4XFleetcrawlPurchaseKind kind)
        {
            var color = kind switch
            {
                Space4XFleetcrawlPurchaseKind.DamageBoost => new Color(1f, 0.45f, 0.23f, 1f),
                Space4XFleetcrawlPurchaseKind.CooldownTrim => new Color(0.24f, 0.72f, 1f, 1f),
                Space4XFleetcrawlPurchaseKind.Heal => new Color(0.26f, 0.95f, 0.46f, 1f),
                _ => new Color(0.93f, 0.88f, 0.26f, 1f)
            };

            ApplyColor(node.BodyRenderer, color, emissionMultiplier: 0.16f);
            ApplyColor(node.BeaconRenderer, color, emissionMultiplier: 0.26f);
        }

        private static void ApplyColor(Renderer renderer, Color color, float emissionMultiplier)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            var material = renderer.material;
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * emissionMultiplier);
            }
        }

        private static uint HashEnemyDrop(uint seed, int roomIndex, int waveIndex, float3 position)
        {
            var quantized = new int2((int)math.round(position.x * 10f), (int)math.round(position.z * 10f));
            var local = math.hash(new int4(roomIndex + 17, waveIndex + 31, quantized.x, quantized.y));
            return math.hash(new uint2(seed, local));
        }

        private static uint HashRewardDrop(uint seed, int roomIndex, int delta, int ordinal)
        {
            return math.hash(new uint4(seed, (uint)(roomIndex + 1), (uint)(delta + 53), (uint)(ordinal + 3) * 7919u));
        }

        private static float3 ComputeDeterministicOffset(uint key, float minRadius, float maxRadius)
        {
            var angle01 = (key & 0xFFFFu) / 65535f;
            var radius01 = ((key >> 16) & 0xFFFFu) / 65535f;
            var angle = angle01 * (math.PI * 2f);
            var radius = math.lerp(minRadius, maxRadius, radius01);
            return new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
        }

        private void SetAllVisualsVisible(bool visible)
        {
            SetShopVisible(visible);
            for (var i = 0; i < _orbs.Count; i++)
            {
                var orb = _orbs[i];
                if (orb?.Root != null)
                {
                    orb.Root.SetActive(visible);
                }
            }
        }

        private void SetShopVisible(bool visible)
        {
            for (var i = 0; i < _shopNodes.Length; i++)
            {
                var node = _shopNodes[i];
                if (node?.Root != null)
                {
                    node.Root.SetActive(visible);
                }
            }
        }

        private void DestroyOrbVisuals()
        {
            for (var i = 0; i < _orbs.Count; i++)
            {
                if (_orbs[i]?.Root != null)
                {
                    Destroy(_orbs[i].Root);
                }
            }

            _orbs.Clear();
            _destroyedEnemySeen.Clear();
        }

        private void DestroyShopVisuals()
        {
            for (var i = 0; i < _shopNodes.Length; i++)
            {
                if (_shopNodes[i]?.Root != null)
                {
                    Destroy(_shopNodes[i].Root);
                }

                _shopNodes[i] = null;
            }
        }

        private bool TryEnsureQueries()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (_queriesReady && world == _world)
            {
                return true;
            }

            _world = world;
            _entityManager = world.EntityManager;
            _scenarioQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>());
            _directorQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XFleetcrawlDirectorState>(),
                ComponentType.ReadOnly<Space4XFleetcrawlRoom>());
            _flagshipQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerFlagshipTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _destroyedEnemyQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XRunEnemyDestroyedCounted>(),
                ComponentType.ReadOnly<Space4XRunEnemyTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _queriesReady = true;
            return true;
        }
    }
}
