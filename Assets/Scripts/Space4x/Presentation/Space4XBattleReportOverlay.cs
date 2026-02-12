using Space4X.Registry;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Presentation
{
    /// <summary>
    /// Lightweight runtime overlay to display the latest <see cref="Space4XBattleReport"/> snapshot.
    /// This is intentionally simple (OnGUI) so we can iterate quickly before a proper UI pass.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class Space4XBattleReportOverlay : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.F9;

        private bool _open;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        private bool _stylesReady;

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                _open = !_open;
            }
        }

        private void OnGUI()
        {
            if (!_open)
            {
                return;
            }

            EnsureStyles();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                GUI.Label(new Rect(12, 12, 600, 20), "Battle Report: ECS world unavailable", _labelStyle);
                return;
            }

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XBattleReport>());
            if (query.IsEmptyIgnoreFilter)
            {
                GUI.Label(new Rect(12, 12, 600, 20), "Battle Report: none yet (run a battle / scenario)", _labelStyle);
                return;
            }

            var report = query.GetSingleton<Space4XBattleReport>();
            var entity = query.GetSingletonEntity();

            var rect = new Rect(12, 12, 520, 240);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            var x = rect.x + 12;
            var y = rect.y + 10;

            GUI.Label(new Rect(x, y, rect.width - 24, 24), "Battle Report", _headerStyle);
            y += 26;

            GUI.Label(new Rect(x, y, rect.width - 24, 18), $"ticks={report.BattleStartTick}..{report.BattleEndTick} winner_side={report.WinnerSide}", _labelStyle);
            y += 18;
            GUI.Label(new Rect(x, y, rect.width - 24, 18), $"combatants={report.TotalCombatants} destroyed={report.TotalDestroyed} alive={report.TotalAlive}", _labelStyle);
            y += 18;
            GUI.Label(new Rect(x, y, rect.width - 24, 18), $"shots_fired={report.ShotsFired} shots_hit={report.ShotsHit}", _labelStyle);
            y += 18;
            GUI.Label(new Rect(x, y, rect.width - 24, 18), $"damage_dealt={report.DamageDealtTotal:0.0} damage_received={report.DamageReceivedTotal:0.0}", _labelStyle);
            y += 22;

            if (em.HasBuffer<Space4XBattleReportSide>(entity))
            {
                var sides = em.GetBuffer<Space4XBattleReportSide>(entity);
                for (var i = 0; i < sides.Length && i < 8; i++)
                {
                    var s = sides[i];
                    GUI.Label(
                        new Rect(x, y, rect.width - 24, 18),
                        $"side={s.Side} ships {s.ShipsAlive}/{s.ShipsTotal} destroyed={s.ShipsDestroyed} hull_ratio={s.HullRatio:0.00} dealt={s.DamageDealt:0.0} recv={s.DamageReceived:0.0}",
                        _labelStyle);
                    y += 18;
                }
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 1f) }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 1f) }
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft
            };

            _stylesReady = true;
        }
    }
}

