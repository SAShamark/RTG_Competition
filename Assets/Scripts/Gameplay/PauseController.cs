using System;
using Gameplay.Entities.Characters.Player;
using Services;
using UI.Popups;
using UI.Popups.Variants.MainMenu;
using UI.Popups.Variants.Pause;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    [Serializable]
    public sealed class PauseController
    {
        private static bool _isRestarting;

        [SerializeField] private bool _showMenuOnStart = true;

        private IPopupsManager _popups;
        private FirstPersonController _player;
        private PausePopup _popup;
        private MainMenuPopup _menu;
        private PausePopup _death;
        private int _openPopups;
        private int _lastClosedFrame = -1;
        private float _savedTimeScale;
        private bool _wasPlayerEnabled;
        private CursorLockMode _savedLock;
        private bool _savedCursorVisible;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public static bool IsPaused { get; private set; }

        public event Action Respawned;

        public void Init(IPopupsManager popups, FirstPersonController player)
        {
            _popups = popups;
            _player = player;
            _spawnPosition = player.transform.position;
            _spawnRotation = player.transform.rotation;
            _popups.PopupShowed += HandlePopupShowed;
            _popups.PopupHidden += HandlePopupHidden;
            if (_showMenuOnStart && !_isRestarting)
            {
                ShowMenu();
            }

            _isRestarting = false;
        }

        public void Dispose()
        {
            if (_menu != null)
            {
                ReleaseMenu();
            }

            if (_popup != null)
            {
                ReleasePopup();
            }

            if (_death != null)
            {
                ReleaseDeath();
            }

            _popups.PopupShowed -= HandlePopupShowed;
            _popups.PopupHidden -= HandlePopupHidden;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void Tick()
        {
            if (_menu != null || !GameInput.CancelPressed || _openPopups > 0 || _lastClosedFrame == Time.frameCount)
            {
                return;
            }

            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void ShowDeath()
        {
            if (_popup != null)
            {
                Resume();
            }

            Freeze();
            _death = _popups.ShowPopup<PausePopup>(PopupTypes.Death);
            _death.RestartChosen += Respawn;
            _death.MainMenuChosen += HandleMainMenuChosen;
        }

        private void Respawn()
        {
            PausePopup death = _death;
            ReleaseDeath();
            death.CloseTrigger();
            Unfreeze();
            _player.Teleport(_spawnPosition, _spawnRotation);
            Respawned?.Invoke();
        }

        private void ReleaseDeath()
        {
            _death.RestartChosen -= Respawn;
            _death.MainMenuChosen -= HandleMainMenuChosen;
            _death = null;
        }

        private void ShowMenu()
        {
            Freeze();
            _menu = _popups.ShowPopup<MainMenuPopup>(PopupTypes.MainMenu);
            _menu.NewGameChosen += CloseMenu;
        }

        private void CloseMenu()
        {
            MainMenuPopup menu = _menu;
            ReleaseMenu();
            menu.CloseTrigger();
            Unfreeze();
        }

        private void ReleaseMenu()
        {
            _menu.NewGameChosen -= CloseMenu;
            _menu = null;
        }

        private void Pause()
        {
            Freeze();
            _popup = _popups.ShowPopup<PausePopup>(PopupTypes.Pause);
            _popup.Continued += Resume;
            _popup.RestartChosen += HandleRestartChosen;
            _popup.MainMenuChosen += HandleMainMenuChosen;
        }

        private void Resume()
        {
            PausePopup popup = _popup;
            ReleasePopup();
            popup.CloseTrigger();
            Unfreeze();
        }

        private void HandleRestartChosen() => Reload(true);

        private void HandleMainMenuChosen() => Reload(false);

        private void Reload(bool isRestarting)
        {
            _isRestarting = isRestarting;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReleasePopup()
        {
            _popup.Continued -= Resume;
            _popup.RestartChosen -= HandleRestartChosen;
            _popup.MainMenuChosen -= HandleMainMenuChosen;
            _popup = null;
        }

        private void Freeze()
        {
            IsPaused = true;
            _savedTimeScale = Time.timeScale;
            _wasPlayerEnabled = _player.enabled;
            _savedLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Unfreeze()
        {
            IsPaused = false;
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
            _player.enabled = _wasPlayerEnabled;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedCursorVisible;
        }

        private void HandlePopupShowed(PopupTypes type)
        {
            if (type != PopupTypes.Pause)
            {
                _openPopups++;
            }
        }

        private void HandlePopupHidden(PopupTypes type)
        {
            if (type != PopupTypes.Pause)
            {
                _openPopups--;
                _lastClosedFrame = Time.frameCount;
            }
        }
    }
}
