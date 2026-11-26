using System;
using System.Collections.Generic;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters;
using Gameplay.Entities.Environment;
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
        [SerializeField] private FireHoseControl _hose;
        [SerializeField] private HoseCrewControl[] _crew;
        [SerializeField] private FireSource[] _hiddenFires;

        private GameplayScreen _screen;
        private TextLibrary _texts;
        private FireSource[] _fires;
        private FireSource[] _facadeFires;
        private NavigationGuide _navigation;
        private HoseStep _step;
        private float _startHeat;
        private float _sinceComplete;
        private int _outCount;

        public bool IsRunning { get; private set; }

        public event Action Completed;

        public void Init(GameplayScreen screen, TextLibrary texts, FireSource[] fires, NavigationGuide navigation)
        {
            _screen = screen;
            _texts = texts;
            _fires = fires;
            _navigation = navigation;
            _hose.SetLocked(true);
            var facade = new List<FireSource>(fires);
            foreach (FireSource hidden in _hiddenFires)
            {
                facade.Remove(hidden);
            }

            _facadeFires = facade.ToArray();
            foreach (HoseCrewControl member in _crew)
            {
                member.gameObject.SetActive(false);
            }
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
            _hose.SetLocked(false);
            foreach (FireSource fire in _fires)
            {
                fire.SetReignite(false);
                fire.Extinguished += HandleFireOut;
            }

            foreach (HoseCrewControl member in _crew)
            {
                member.gameObject.SetActive(true);
                member.Begin(_facadeFires);
            }

            _startHeat = Heat();
            ShowStep(_hose.Step);
            _screen.ShowFireProgress(_config.ShownAtStart);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            if (_hose.Step != _step)
            {
                ShowStep(_hose.Step);
            }

            // The bar fills by the share of the heat that has to go, so it reaches its end exactly when enough is out.
            float share = _startHeat > 0f ? Mathf.Clamp01((1f - Heat() / _startHeat) / _config.CompleteShare) : 1f;
            // Fires round the sides cannot be reached from the street: they die down in step with the facade.
            foreach (FireSource hidden in _hiddenFires)
            {
                hidden.BurnDown(1f - share);
            }

            _screen.SetFireProgress(Mathf.Lerp(_config.ShownAtStart, _config.ShownAtEnd, share));
            if (share < 1f)
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
            foreach (FireSource fire in _facadeFires)
            {
                heat += fire.gameObject.activeInHierarchy && !fire.IsOut ? fire.Intensity : 0f;
            }

            return heat;
        }

        private void ShowStep(HoseStep step)
        {
            _step = step;
            _screen.SetObjective(_texts[_config.ObjectiveFor(step)]);
            if (step == HoseStep.Ready)
            {
                _navigation.Clear();
                return;
            }

            _navigation.SetRoute(new[] { _hose.StepPoint }, null, _texts[_config.LabelFor(step)]);
        }

        private void Finish()
        {
            IsRunning = false;
            Unsubscribe();
            _navigation.Clear();
            foreach (HoseCrewControl member in _crew)
            {
                member.Stop();
            }

            foreach (FireSource fire in _fires)
            {
                if (fire.gameObject.activeInHierarchy)
                {
                    fire.ForceOut();
                }
            }

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
