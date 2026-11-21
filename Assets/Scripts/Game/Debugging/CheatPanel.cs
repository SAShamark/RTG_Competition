using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Debugging
{
    public class CheatPanel : MonoBehaviour
    {
        [Serializable]
        public struct Teleport
        {
            public string Label;
            public Transform Point;
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
        private Rect _window = new(20f, 70f, 280f, 10f);

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
                    _entryPoint.CheatSkipIntro();
                    break;
                case StartStage.Floor1:
                    _entryPoint.CheatOpenEntrance();
                    TeleportTo(1);
                    break;
                case StartStage.Grandma:
                    _entryPoint.CheatOpenEntrance();
                    _entryPoint.CheatPutOutFloor1();
                    _entryPoint.CheatGiveTools();
                    TeleportTo(2);
                    break;
                case StartStage.Neighbor:
                    _entryPoint.CheatRescueGrandma();
                    _entryPoint.CheatGiveTools();
                    TeleportTo(3);
                    break;
                case StartStage.GasBurst:
                    TeleportTo(4);
                    _entryPoint.CheatRescueNeighbor();
                    break;
                case StartStage.Finale:
                    TeleportTo(4);
                    _entryPoint.CheatRescueNeighbor();
                    _entryPoint.PlayFinale();
                    break;
                case StartStage.Results:
                    _entryPoint.CheatSkipIntro();
                    _entryPoint.ShowResults();
                    break;
            }
        }

        private void TeleportTo(int index)
        {
            if (index < _teleports.Length)
            {
                _entryPoint.CheatTeleport(_teleports[index].Point);
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
                if (Cursor.visible && GUI.Button(new Rect(Screen.width - 90f, 8f, 80f, 24f), "Cheats"))
                {
                    SetOpen(true);
                }

                return;
            }

            _window = GUILayout.Window(7317, _window, DrawWindow, "Cheats  (F1)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Стан місії");
            Button("Пропустити інтро", () => _entryPoint.CheatSkipIntro());
            Button("Відкрити під'їзд (без сцени з дядьком)", () => _entryPoint.CheatOpenEntrance());
            Button("Загасити 1-й поверх", () => _entryPoint.CheatPutOutFloor1());
            Button("Дати лом і вогнегасник, повний заряд", () => _entryPoint.CheatGiveTools());
            Button("Бабуся врятована (2-й поверх пройдено)", () => _entryPoint.CheatRescueGrandma());
            Button("Відкрити комору (без відмички)", () => _entryPoint.CheatOpenPantry());
            Button("Дати обидва маховики", () => _entryPoint.CheatGiveWheels());
            Button("Сусід врятований → зрив газу → фінал", () => { SetOpen(false); TeleportTo(4); _entryPoint.CheatRescueNeighbor(); });

            GUILayout.Space(6f);
            GUILayout.Label("Фінал");
            Button("Одразу вибух і фінал", () => { SetOpen(false); TeleportTo(4); _entryPoint.CheatRescueNeighbor(); _entryPoint.PlayFinale(); });
            Button("Одразу результати", () => { _entryPoint.ShowResults(); _isOpen = false; });

            if (_teleports.Length > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Телепорт");
                foreach (Teleport teleport in _teleports)
                {
                    if (GUILayout.Button(teleport.Label))
                    {
                        _entryPoint.CheatTeleport(teleport.Point);
                        SetOpen(false);
                    }
                }
            }

            GUILayout.Space(6f);
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
