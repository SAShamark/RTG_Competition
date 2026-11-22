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

        public CinematicPresenter(UIManager uiManager)
        {
            _uiManager = uiManager;
        }

        public void Present(ICinematic cinematic)
        {
            _presented = cinematic;
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Intro);
            var cutsceneScreen = (CutsceneScreen)_uiManager.ScreensManager.GetScreen(ScreenTypes.Intro);
            cinematic.Finished += HandleFinished;
            cutsceneScreen.Present(cinematic);
        }

        public void Dispose()
        {
            if (_presented != null)
            {
                _presented.Finished -= HandleFinished;
            }
        }

        private void HandleFinished()
        {
            _presented.Finished -= HandleFinished;
            _presented = null;
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
        }
    }
}
