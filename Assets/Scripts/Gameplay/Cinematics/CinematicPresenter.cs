using Services.Audio;
using System;
using UI.Managers;
using UI.Screens;
using UI.Screens.Variants.Cutscene;

namespace Gameplay.Cinematics
{
    public sealed class CinematicPresenter : IDisposable
    {
        private readonly UIManager _uiManager;
        private ICinematic _presented;
        private Action _presentedFinished;

        public CinematicPresenter(UIManager uiManager)
        {
            _uiManager = uiManager;
        }

        public void Present(ICinematic cinematic)
        {
            Release();
            _presented = cinematic;
            _presentedFinished = () => HandleFinished(cinematic);
            cinematic.Finished += _presentedFinished;
            AudioService.Instance.SetDucked(true);
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Intro);
            var cutsceneScreen = (CutsceneScreen)_uiManager.ScreensManager.GetScreen(ScreenTypes.Intro);
            cutsceneScreen.Present(cinematic);
        }

        public void Dispose()
        {
            Release();
        }

        private void Release()
        {
            if (_presented != null)
            {
                _presented.Finished -= _presentedFinished;
                _presented = null;
            }
        }

        // A cinematic often starts the next one from its own Finished event; only the one on screen may close the screen.
        private void HandleFinished(ICinematic cinematic)
        {
            if (cinematic != _presented)
            {
                return;
            }

            Release();
            AudioService.Instance.SetDucked(false);
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
        }
    }
}
