using Gameplay.Dialogue;
using Gameplay.Effects.Puddle;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Systems;
using UI.Managers;
using UI.Screens;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Sandbox
{
    [DefaultExecutionOrder(100)]
    public sealed class SandboxEntryPoint : MonoBehaviour
    {
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private EvacuationWindow[] _evacuationWindows;
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private PuddleControl _puddle;
        [SerializeField] private FireHeatLogic _fireHeat = new();
        [SerializeField] private DialogLibrary _dialogs;
        [SerializeField] private ResidentComplaints _complaints = new();

        private GameplayScreen _screen;

        private void Start()
        {
            UIManager uiManager = UIManager.Instance;
            uiManager.Init();
            uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
            _screen = (GameplayScreen)uiManager.ScreensManager.GetScreen(ScreenTypes.Gameplay);

            _fireHeat.Init(_player.Head);
            _screen.Init(_player, _fireHeat, _texts, Camera.main);
            _screen.SetHudVisible(true);
            _screen.SetCrosshairVisible(true);

            foreach (EvacuationWindow window in _evacuationWindows)
            {
                window.Init(_player.Carry);
            }

            _complaints.Init(_screen, _texts, _dialogs);
            _player.Hose.WaterLanded += _puddle.Pour;
        }

        private void Update()
        {
            _fireHeat.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _player.Hose.WaterLanded -= _puddle.Pour;
            _complaints.Dispose();
            _screen?.Dispose();
        }
    }
}
