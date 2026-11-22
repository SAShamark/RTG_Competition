using Gameplay.Entities.Player;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

namespace Debugging
{
    public sealed class CheatPanel : MonoBehaviour
    {
        private static readonly Rect InitialWindow = new(20f, 70f, 280f, 10f);
        private const int NeighborTeleport = 3;
        private const int KitchenTeleport = 4;
        private const int WindowId = 7317;
        private const float ToggleMargin = 8f;
        private static readonly Vector2 ToggleSize = new(80f, 24f);
        private const float SectionSpacing = 6f;

        [Serializable]
        public struct Teleport
        {
            [SerializeField, FormerlySerializedAs("Label")] private string _label;
            [SerializeField, FormerlySerializedAs("Point")] private Transform _point;

            public string Label => _label;
            public Transform Point => _point;
        }

        public enum StartStage
        {
            FromStart = 0,
            StreetNoIntro = 1,
            Floor1 = 2,
            Grandma = 3,
            Neighbor = 4,
            GasBurst = 5,
            Finale = 6,
            Results = 7,
        }

        [Tooltip("Editor / development builds: on Play, the mission is moved to this stage automatically")]
        [SerializeField] private StartStage _startStage = StartStage.FromStart;
        [SerializeField] private EntryPoint _entryPoint;
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private Teleport[] _teleports;

        private bool _isOpen;
        private bool _playerWasEnabled;
        private Rect _window = InitialWindow;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private System.Collections.IEnumerator Start()
        {
            if (_startStage == StartStage.FromStart)
            {
                yield break;
            }

            yield return null;
            ApplyStage(_startStage);
        }

        private void ApplyStage(StartStage stage)
        {
            switch (stage)
            {
                case StartStage.StreetNoIntro:
                    _entryPoint.Gameplay.Cheats.SkipIntro();
                    break;
                case StartStage.Floor1:
                    _entryPoint.Gameplay.Cheats.OpenEntrance();
                    TeleportTo(1);
                    break;
                case StartStage.Grandma:
                    _entryPoint.Gameplay.Cheats.OpenEntrance();
                    _entryPoint.Gameplay.Cheats.PutOutFloor1();
                    _entryPoint.Gameplay.Cheats.GiveTools();
                    TeleportTo(2);
                    break;
                case StartStage.Neighbor:
                    _entryPoint.Gameplay.Cheats.RescueGrandma();
                    _entryPoint.Gameplay.Cheats.GiveTools();
                    TeleportTo(NeighborTeleport);
                    break;
                case StartStage.GasBurst:
                    TeleportTo(KitchenTeleport);
                    _entryPoint.Gameplay.Cheats.RescueNeighbor();
                    break;
                case StartStage.Finale:
                    TeleportTo(KitchenTeleport);
                    _entryPoint.Gameplay.Cheats.RescueNeighbor();
                    _entryPoint.Gameplay.PlayFinale();
                    break;
                case StartStage.Results:
                    _entryPoint.Gameplay.Cheats.SkipIntro();
                    _entryPoint.Gameplay.ShowResults();
                    break;
            }
        }

        private void TeleportTo(int index)
        {
            if (index < _teleports.Length)
            {
                _entryPoint.Gameplay.Cheats.Teleport(_teleports[index].Point);
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb.f1Key.wasPressedThisFrame)
            {
                SetOpen(!_isOpen);
            }
        }

        private void SetOpen(bool isOpen)
        {
            _isOpen = isOpen;
            if (isOpen)
            {
                _playerWasEnabled = _player.enabled;
                _player.enabled = false;
            }
            else if (_playerWasEnabled)
            {
                _player.enabled = true;
            }

            Cursor.lockState = isOpen ? CursorLockMode.None : (_player.enabled ? CursorLockMode.Locked : Cursor.lockState);
            Cursor.visible = isOpen || (!_player.enabled);
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                if (Cursor.visible && GUI.Button(new Rect(Screen.width - ToggleSize.x - ToggleMargin, ToggleMargin, ToggleSize.x, ToggleSize.y), "Cheats"))
                {
                    SetOpen(true);
                }

                return;
            }

            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Cheats  (F1)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Стан місії");
            Button("Пропустити інтро", () => _entryPoint.Gameplay.Cheats.SkipIntro());
            Button("Відкрити під'їзд (без сцени з дядьком)", () => _entryPoint.Gameplay.Cheats.OpenEntrance());
            Button("Загасити 1-й поверх", () => _entryPoint.Gameplay.Cheats.PutOutFloor1());
            Button("Дати лом і вогнегасник, повний заряд", () => _entryPoint.Gameplay.Cheats.GiveTools());
            Button("Бабуся врятована (2-й поверх пройдено)", () => _entryPoint.Gameplay.Cheats.RescueGrandma());
            Button("Відкрити комору (без відмички)", () => _entryPoint.Gameplay.Cheats.OpenPantry());
            Button("Дати обидва маховики", () => _entryPoint.Gameplay.Cheats.GiveWheels());
            Button("Сусід врятований → зрив газу → фінал", () => { SetOpen(false); TeleportTo(KitchenTeleport); _entryPoint.Gameplay.Cheats.RescueNeighbor(); });

            GUILayout.Space(SectionSpacing);
            GUILayout.Label("Фінал");
            Button("Одразу вибух і фінал", () => { SetOpen(false); TeleportTo(KitchenTeleport); _entryPoint.Gameplay.Cheats.RescueNeighbor(); _entryPoint.Gameplay.PlayFinale(); });
            Button("Одразу результати", () => { _entryPoint.Gameplay.ShowResults(); _isOpen = false; });

            if (_teleports.Length > 0)
            {
                GUILayout.Space(SectionSpacing);
                GUILayout.Label("Телепорт");
                foreach (Teleport teleport in _teleports)
                {
                    if (GUILayout.Button(teleport.Label))
                    {
                        _entryPoint.Gameplay.Cheats.Teleport(teleport.Point);
                        SetOpen(false);
                    }
                }
            }

            GUILayout.Space(SectionSpacing);
            if (GUILayout.Button("Закрити"))
            {
                SetOpen(false);
            }

            GUI.DragWindow();
        }

        private void Button(string label, Action action)
        {
            if (GUILayout.Button(label))
            {
                action();
            }
        }
#endif
    }
}
