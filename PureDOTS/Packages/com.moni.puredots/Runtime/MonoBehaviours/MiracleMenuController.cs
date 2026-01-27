using PureDOTS.Runtime.Miracles;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace PureDOTS.MonoBehaviours
{
    /// <summary>
    /// Simple UI controller for miracle selection menu.
    /// Provides buttons for Heal, Smite, and Rain miracles.
    /// </summary>
    public sealed class MiracleMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button healButton;
        [SerializeField] private Button smiteButton;
        [SerializeField] private Button rainButton;
        [SerializeField] private Button deselectButton;

        private World _world;
        private EntityManager _entityManager;
        private Entity _selectionEntity;

        void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogWarning("[MiracleMenuController] DefaultGameObjectInjectionWorld not found.", this);
                enabled = false;
                return;
            }
            _entityManager = _world.EntityManager;
        }

        void OnEnable()
        {
            // Hook up button events
            if (healButton != null)
            {
                healButton.onClick.AddListener(() => SelectMiracle(MiracleId.Heal));
            }
            if (smiteButton != null)
            {
                smiteButton.onClick.AddListener(() => SelectMiracle(MiracleId.Fire)); // Fire replaces Smite
            }
            if (rainButton != null)
            {
                rainButton.onClick.AddListener(() => SelectMiracle(MiracleId.Rain));
            }
            if (deselectButton != null)
            {
                deselectButton.onClick.AddListener(() => SelectMiracle(MiracleId.None));
            }
        }

        void OnDisable()
        {
            // Unhook button events
            if (healButton != null)
            {
                healButton.onClick.RemoveAllListeners();
            }
            if (smiteButton != null)
            {
                smiteButton.onClick.RemoveAllListeners();
            }
            if (rainButton != null)
            {
                rainButton.onClick.RemoveAllListeners();
            }
            if (deselectButton != null)
            {
                deselectButton.onClick.RemoveAllListeners();
            }
        }

        void Start()
        {
            EnsureSelectionEntity();
        }

        void EnsureSelectionEntity()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            var query = _entityManager.CreateEntityQuery(typeof(MiracleSelection));
            if (query.IsEmpty)
            {
                // Create singleton entity if it doesn't exist
                _selectionEntity = _entityManager.CreateEntity(typeof(MiracleSelection));
                _entityManager.SetComponentData(_selectionEntity, new MiracleSelection
                {
                    SelectedMiracleId = (int)MiracleId.None
                });
            }
            else
            {
                _selectionEntity = query.GetSingletonEntity();
            }
            query.Dispose();
        }

        void SelectMiracle(MiracleId miracleId)
        {
            if (_world == null || !_world.IsCreated || _selectionEntity == Entity.Null)
            {
                EnsureSelectionEntity();
                if (_selectionEntity == Entity.Null)
                {
                    return;
                }
            }

            var selection = _entityManager.GetComponentData<MiracleSelection>(_selectionEntity);
            selection.SelectedMiracleId = (int)miracleId;
            _entityManager.SetComponentData(_selectionEntity, selection);

            Debug.Log($"[MiracleMenuController] Selected miracle: {miracleId}");
        }
    }
}

