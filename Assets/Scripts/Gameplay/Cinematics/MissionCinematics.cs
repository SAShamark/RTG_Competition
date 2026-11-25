using System;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using Services.Audio;
using UI.Managers;
using UI.Popups.Variants.Results;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Cinematics
{
    [Serializable]
    public sealed class MissionCinematics : IDisposable
    {
        [SerializeField] private IntroCutscene _introCutscene;
        [SerializeField] private UncleBurstCutscene _uncleBurstCutscene;
        [SerializeField] private EvacuationThrowCutscene _throwCutscene;
        [SerializeField] private GasBurstCutscene _gasBurstCutscene;
        [SerializeField] private FinaleCutscene _finale;
        [SerializeField] private ResultsPhotoRig _photoRig = new();

        private CinematicPresenter _presenter;
        private GameplayScreen _screen;
        private FirstPersonController _player;

        public EvacuationThrowCutscene Throw => _throwCutscene;
        public ResultsPhotoRig PhotoRig => _photoRig;
        public FireSource[] HouseFires => _finale.HouseFires;

        public event Action IntroFinished;
        public event Action UncleBurstFinished;
        public event Action GasBurstFinished;
        public event Action FinaleJumpFinished;
        public event Action<Texture> FinaleFinished;

        public void Initialize(UIManager uiManager, GameplayScreen screen, FirstPersonController player, Camera camera, AudioService audio)
        {
            _presenter = new CinematicPresenter(uiManager, audio);
            _screen = screen;
            _player = player;
            _uncleBurstCutscene.Init(player, camera);
            _finale.Init(camera);

            _introCutscene.Finished += HandleIntroFinished;
            _uncleBurstCutscene.Finished += HandleUncleBurstFinished;
            _gasBurstCutscene.Finished += HandleGasBurstFinished;
            _finale.Finished += HandleFinaleFinished;
        }

        public void Dispose()
        {
            _introCutscene.Finished -= HandleIntroFinished;
            _uncleBurstCutscene.Finished -= HandleUncleBurstFinished;
            _gasBurstCutscene.Finished -= HandleGasBurstFinished;
            _finale.Finished -= HandleFinaleFinished;
            _presenter.Dispose();
        }

        public bool TryPresentIntro()
        {
            if (!_introCutscene.IsPlaying)
            {
                return false;
            }

            Present(_introCutscene);
            return true;
        }

        public void PlayUncleBurst()
        {
            _uncleBurstCutscene.Play();
            Present(_uncleBurstCutscene);
        }

        public void PlayGasBurst()
        {
            _gasBurstCutscene.Play(_player);
            Present(_gasBurstCutscene);
        }

        public void PlayFinale()
        {
            _finale.Play(_player);
            Present(_finale);
        }

        public void PlayFinaleEnding()
        {
            _finale.PlayEnding(_player);
            Present(_finale);
        }

        public void ReleaseFinale() => _finale.Release(_player);

        public void Present(ICinematic cinematic)
        {
            _screen.SetCrosshairVisible(false);
            _presenter.Present(cinematic);
        }

        private void HandleIntroFinished() => IntroFinished?.Invoke();

        private void HandleUncleBurstFinished() => UncleBurstFinished?.Invoke();

        private void HandleGasBurstFinished() => GasBurstFinished?.Invoke();

        private void HandleFinaleFinished()
        {
            if (_finale.IsEnding)
            {
                FinaleFinished?.Invoke(_finale.CapturedFrame);
            }
            else
            {
                FinaleJumpFinished?.Invoke();
            }
        }
    }
}
