using UI.Screens.Base;
using UI.Widgets.Hud;
using UnityEngine;

namespace UI.Screens.Variants.Gameplay
{
    public class GameplayScreen : BaseScreen
    {
        public const int SlotCrowbar = 0;
        public const int SlotHose = 1;
        public const int SlotExtinguisher = 2;
        public const int SlotRadio = 3;

        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private GameObject _crosshair;

        [Header("Widgets")]
        [SerializeField] private ObjectiveWidget _objective;
        [SerializeField] private CounterWidget _rescued;
        [SerializeField] private CounterWidget _wheels;
        [SerializeField] private FireProgressWidget _fireProgress;
        [SerializeField] private DamageWidget _damage;
        [SerializeField] private InteractionPromptWidget _prompt;
        [SerializeField] private TargetFrameWidget _targetFrame;
        [SerializeField] private PartnerDialogWidget _partner;
        [SerializeField] private HotbarWidget _hotbar;
        [SerializeField] private ToastWidget _toasts;
        [SerializeField] private HeatVignetteWidget _heat;

        private int _objectiveStep = 1;
        private int _objectiveTotal = 3;

        public HotbarWidget Hotbar => _hotbar;

        public void SetObjective(int step, int total, string text)
        {
            _objectiveStep = step;
            _objectiveTotal = total;
            _objective.SetObjective(step, total, text);
        }

        public void SetObjective(string text) => _objective.SetObjective(_objectiveStep, _objectiveTotal, text);

        public void SetObjectiveVisible(bool isVisible) => SetHudVisible(isVisible);

        public void SetHudVisible(bool isVisible)
        {
            _hudRoot.SetActive(isVisible);
        }

        public void SetRescued(int value, int total) => _rescued.Set(value, total);

        public void SetWheels(int value, int total)
        {
            _wheels.gameObject.SetActive(true);
            _wheels.Set(value, total);
        }

        public void HideWheels()
        {
            _wheels.gameObject.SetActive(false);
        }

        public void SetFireProgress(float extinguished01) => _fireProgress.SetProgress(extinguished01);

        public void AddMoney(int amount) => _damage.Add(amount);

        public void SetHeat(float intensity01) => _heat.SetIntensity(intensity01);

        public void SetCrosshairVisible(bool isVisible)
        {
            _crosshair.SetActive(isVisible);

            if (!isVisible)
            {
                SetInteractionHint(null);
                SetInteractionTarget(null);
            }
        }

        public void SetInteractionHint(string hint) => _prompt.Show(hint);

        public void SetInteractionTarget(Component target) => _targetFrame.SetTarget(target);

        public void PartnerSay(string line, float hold = 3f) => _partner.Say(line, hold);

        public void CharacterSay(string speaker, string line, float hold = 3f, Transform anchor = null) =>
            _partner.Say(line, hold, speaker, anchor);

        public void ClearDialog() => _partner.Clear();

        public void ShowToast(string message) => _toasts.Show(message);
    }
}
