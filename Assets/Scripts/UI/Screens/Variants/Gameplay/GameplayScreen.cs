using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UI.Screens.Variants.Cutscene.Widgets;
using UI.Screens.Variants.Gameplay.Widgets;
using UI.Screens.Base;
using UnityEngine;

namespace UI.Screens.Variants.Gameplay
{
    public sealed class GameplayScreen : BaseScreen
    {
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private GameObject _crosshair;

        [Header("Widgets")]
        [SerializeField] private ObjectiveWidget _objective;
        [SerializeField] private RescueNoticeWidget _rescued;
        [SerializeField] private InteractionPromptWidget _prompt;
        [SerializeField] private TargetFrameWidget _targetFrame;
        [SerializeField] private PartnerDialogWidget _partner;
        [SerializeField] private HotbarWidget _hotbar;
        [SerializeField] private NavMarkerWidget _navMarker;
        [SerializeField] private HotbarSlot _activeSlot;
        [SerializeField] private GameObject _activePanel;
        [SerializeField] private HotbarSlot _radio;
        [SerializeField] private GameObject _dropHint;
        [SerializeField] private ToastWidget _toasts;
        [SerializeField] private HeatVignetteWidget _heat;
        [SerializeField] private PressureGaugeWidget _pressure;
        [SerializeField] private FireProgressWidget _fireProgress;

        private FirstPersonController _player;
        private FireHeatLogic _fireHeat;
        private TextLibrary _texts;
        private bool _extinguisherHintShown;
        private bool _carryHintShown;
        private ClutterItem _heldProp;
        private Rescuable _carriedResident;

        public HotbarWidget Hotbar => _hotbar;
        public NavMarkerWidget NavMarker => _navMarker;

        public void SetObjective(string text) => _objective.SetObjective(text);

        public void UpdateObjectiveText(string text) => _objective.UpdateText(text);

        public void SetObjectiveVisible(bool isVisible) => SetHudVisible(isVisible);

        public void SetHudVisible(bool isVisible)
        {
            _hudRoot.SetActive(isVisible);
        }

        public void ShowRescued(int value, int total) => _rescued.Show(value, total);

        public void HideRescued() => _rescued.Hide();

        public void PulseRadio() => _radio.Pulse();

        private static Sprite Icon(InventoryEntry entry) => entry.IsEmpty ? null : entry.Definition.Icon;

        public void SetDropHint(bool isVisible) => _dropHint.SetActive(isVisible);

        public void SetHeat(float intensity01) => _heat.SetIntensity(intensity01);

        public void ShowFireProgress(float value01)
        {
            _fireProgress.gameObject.SetActive(true);
            _fireProgress.Snap(value01);
        }

        public void SetFireProgress(float value01) => _fireProgress.SetProgress(value01);

        public void HideFireProgress() => _fireProgress.gameObject.SetActive(false);

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

        public void PartnerSay(string line, float hold) => _partner.Say(line, hold);

        public void CharacterSay(string speaker, string line, float hold, Transform anchor = null) =>
            _partner.Say(line, hold, speaker, anchor);

        public void Say(DialogPhrase phrase, Transform anchor = null)
        {
            if (phrase.Character == CharactersType.Partner)
            {
                _partner.Say(phrase.Text, phrase.Hold);
                return;
            }

            _partner.Say(phrase.Text, phrase.Hold, phrase.Speaker, anchor);
        }

        public bool IsDialogBusy => _partner.IsBusy;

        public void ClearDialog() => _partner.Clear();

        public void ShowToast(string message) => _toasts.Show(message);

        public void Init(FirstPersonController player, FireHeatLogic fireHeat, TextLibrary texts, Camera camera)
        {
            _targetFrame.Init(camera);
            _partner.Init(camera);
            _player = player;
            _fireHeat = fireHeat;
            _pressure.Init(player.Extinguisher, player.Axe);
            _texts = texts;
            HideRescued();
            SetHeat(0f);
            HideFireProgress();
            RefreshInventory();

            _player.Interaction.HintChanged += SetInteractionHint;
            _player.Interaction.TargetChanged += SetInteractionTarget;
            _fireHeat.HeatChanged += SetHeat;
            _player.Items.ItemChanged += HandleItemChanged;
            _player.Items.InventoryChanged += RefreshInventory;
            _player.Items.InventoryFull += HandleInventoryFull;
            _player.Hose.RanOut += HandleHoseRanOut;
            _player.PropChanged += HandlePropChanged;
            _player.Carry.CarryChanged += HandleCarryChanged;
        }

        public void Dispose()
        {
            _player.Interaction.HintChanged -= SetInteractionHint;
            _player.Interaction.TargetChanged -= SetInteractionTarget;
            _fireHeat.HeatChanged -= SetHeat;
            _player.Items.ItemChanged -= HandleItemChanged;
            _player.Items.InventoryChanged -= RefreshInventory;
            _player.Items.InventoryFull -= HandleInventoryFull;
            _player.Hose.RanOut -= HandleHoseRanOut;
            _player.PropChanged -= HandlePropChanged;
            _player.Carry.CarryChanged -= HandleCarryChanged;
        }

        private void RefreshInventory()
        {
            for (int i = 0; i < InventoryLogic.SlotCount; i++)
            {
                InventoryEntry slot = _player.Items.Slot(i);
                _hotbar.SetItem(i, Icon(slot), slot.Count);
            }

            RefreshHand();
        }

        private void HandlePropChanged(ClutterItem prop)
        {
            _heldProp = prop;
            RefreshHand();
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            _carriedResident = resident;
            RefreshHand();
            if (resident != null && !_carryHintShown)
            {
                _carryHintShown = true;
                ShowToast(_texts[TextId.ToastCarryHint]);
            }
        }

        private void RefreshHand()
        {
            InventoryEntry hand = _player.Items.Hand;
            bool isCarrying = _carriedResident != null;
            bool isHoldingProp = _heldProp != null;
            Sprite icon = isCarrying ? _carriedResident.Icon : isHoldingProp ? _heldProp.Icon : Icon(hand);

            _activePanel.SetActive(icon != null);
            _activeSlot.SetSelected(icon != null);
            _activeSlot.SetItem(icon, isCarrying || isHoldingProp ? 1 : hand.Count);
            SetDropHint(isCarrying || (!isHoldingProp && _player.Items.HeldTool != null));
            _pressure.SetItem(isCarrying || isHoldingProp ? default : hand.Type);
        }

        private void HandleItemChanged(ItemType item)
        {
            if (item == ItemType.Extinguisher && !_extinguisherHintShown)
            {
                _extinguisherHintShown = true;
                ShowToast(_texts[TextId.ToastExtinguisherHint]);
            }
        }

        private void HandleInventoryFull() => ShowToast(_texts[TextId.ToastInventoryFull]);

        private void HandleHoseRanOut() => ShowToast(_texts[TextId.ToastHoseRanOut]);
    }
}
