using System;
using SystemEnv = System.Environment;
using PureDOTS.Runtime;
using Unity.Entities;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PureDOTS.Runtime.MonoBehaviours
{
    /// <summary>
    /// MonoBehaviour controller for switching simulation scenarios via F1-F4 hotkeys.
    /// Attach to a GameObject in the showcase scene.
    /// </summary>
    public class ScenarioController : MonoBehaviour
    {
        private const string LegacyScenarioEnvVar = "PURE_DOTS_LEGACY_SCENARIO";

        private void Awake()
        {
            if (!IsLegacyProfileEnabled())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
#else
            return;
#endif

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(ScenarioState));
            
            if (!query.TryGetSingletonEntity<ScenarioState>(out var e))
                return;

            var state = em.GetComponentData<ScenarioState>(e);
            bool changed = false;

#if ENABLE_INPUT_SYSTEM
            if (kb.f1Key.wasPressedThisFrame)
            {
                state.Current = ScenarioKind.AllSystemsShowcase;
                changed = true;
            }
            else if (kb.f2Key.wasPressedThisFrame)
            {
                state.Current = ScenarioKind.Space4XPhysicsOnly;
                changed = true;
            }
            else if (kb.f3Key.wasPressedThisFrame)
            {
                state.Current = ScenarioKind.GodgamePhysicsOnly;
                changed = true;
            }
            else if (kb.f4Key.wasPressedThisFrame)
            {
                state.Current = ScenarioKind.HandThrowSandbox;
                changed = true;
            }
#endif

            if (changed)
            {
                em.SetComponentData(e, state);
            }
        }

        private static bool IsLegacyProfileEnabled()
        {
            var value = SystemEnv.GetEnvironmentVariable(LegacyScenarioEnvVar);
            return IsEnabledValue(value);
        }

        private static bool IsEnabledValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
