using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Services.Audio;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission.Systems
{
    [Serializable]
    public sealed class FacadeFirefight
    {
        [SerializeField] private FacadeFirefightConfig _config;

        private GameplayScreen _screen;
        private TextLibrary _texts;
        private FireSource[] _fires;
        private float _startHeat;
        private float _sinceComplete;
        private int _outCount;

        public bool IsRunning { get; private set; }

        public event Action Completed;

        public void Init(GameplayScreen screen, TextLibrary texts, FireSource[] fires)
        {
            _screen = screen;
            _texts = texts;
            _fires = fires;
        }

        public void Dispose()
        {
            if (IsRunning)
            {
                Unsubscribe();
            }
        }

        public void Begin()
        {
            IsRunning = true;
            _sinceComplete = 0f;
            _outCount = 0;
            foreach (FireSource fire in _fires)
            {
                fire.SetReignite(false);
                fire.Extinguished += HandleFireOut;
            }

            _startHeat = Heat();
            _screen.SetObjective(_texts[_config.Objective]);
            _screen.ShowFireProgress(_config.ShownAtStart);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            float share = _startHeat > 0f ? 1f - Heat() / _startHeat : 1f;
            _screen.SetFireProgress(Mathf.Lerp(_config.ShownAtStart, _config.ShownAtEnd, share));
            if (share < _config.CompleteShare)
            {
                return;
            }

            _sinceComplete += deltaTime;
            if (_sinceComplete >= _config.FinishDelay)
            {
                Finish();
            }
        }

        private float Heat()
        {
            float heat = 0f;
            foreach (FireSource fire in _fires)
            {
                heat += fire.gameObject.activeInHierarchy && !fire.IsOut ? fire.Intensity : 0f;
            }

            return heat;
        }

        private void Finish()
        {
            IsRunning = false;
            Unsubscribe();
            _screen.HideFireProgress();
            _screen.ShowToast(_texts[_config.DoneToast]);
            Completed?.Invoke();
        }

        private void Unsubscribe()
        {
            foreach (FireSource fire in _fires)
            {
                fire.Extinguished -= HandleFireOut;
            }
        }

        private void HandleFireOut(FireSource fire)
        {
            float pitch = Mathf.Min(_config.MaxPitch, _config.BasePitch + _config.PitchStep * _outCount);
            _outCount++;
            AudioService.Instance.Play(_config.FireOutSound, pitch);
        }
    }
}
