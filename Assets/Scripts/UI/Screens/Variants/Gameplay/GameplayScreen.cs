using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Player;
using Gameplay.Entities.Residents;
using Gameplay.Mission;
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
        [SerializeField] private HotbarSlot _radio;
        [SerializeField] private GameObject _dropHint;
        [SerializeField] private ToastWidget _toasts;
        [SerializeField] private HeatVignetteWidget _heat;

        private FirstPersonController _player;
        private FireHeatLogic _fireHeat;
        private EvacuationController _evacuation;
        private TextLibrary _texts;
        private int _rescueTotal;
        private bool _extinguisherHintShown;
        private bool _carryHintShown;

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

        public void SetHeldProp(Sprite icon) => _activeSlot.SetItem(icon, 1);

        public void SetInventory(InventoryEntry hand, InventoryEntry[] slots)
        {
            _activeSlot.SetItem(Icon(hand), hand.Count);
            for (int i = 0; i < slots.Length; i++)
            {
                _hotbar.SetItem(i, Icon(slots[i]), slots[i].Count);
            }
        }

        private static Sprite Icon(InventoryEntry entry) => entry.IsEmpty ? null : entry.Definition.Icon;

        public void SetDropHint(bool isVisible) => _dropHint.SetActive(isVisible);

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

        public void ClearDialog() => _partner.Clear();

        public void ShowToast(string message) => _toasts.Show(message);

        public void Init(FirstPersonController player, FireHeatLogic fireHeat, EvacuationController evacuation, int rescueTotal, TextLibrary texts)
        {
            _player = player;
            _fireHeat = fireHeat;
            _evacuation = evacuation;
            _rescueTotal = rescueTotal;
            _texts = texts;
            HideRescued();
            SetHeat(0f);
            RefreshInventory();

            _player.Interaction.HintChanged += SetInteractionHint;
            _player.Interaction.TargetChanged += SetInteractionTarget;
            _fireHeat.HeatChanged += SetHeat;
            _player.Items.ItemChanged += HandleItemChanged;
            _player.Items.InventoryChanged += RefreshInventory;
            _player.Items.InventoryFull += HandleInventoryFull;
            _player.PropChanged += HandlePropChanged;
            _player.Carry.CarryChanged += HandleCarryChanged;
            _evacuation.ResidentRescued += HandleRescued;
            _evacuation.ThrowFinished += HandleThrowFinished;
        }

        public void Dispose()
        {
            _player.Interaction.HintChanged -= SetInteractionHint;
            _player.Interaction.TargetChanged -= SetInteractionTarget;
            _fireHeat.HeatChanged -= SetHeat;
            _player.Items.ItemChanged -= HandleItemChanged;
            _player.Items.InventoryChanged -= RefreshInventory;
            _player.Items.InventoryFull -= HandleInventoryFull;
            _player.PropChanged -= HandlePropChanged;
            _player.Carry.CarryChanged -= HandleCarryChanged;
            _evacuation.ResidentRescued -= HandleRescued;
            _evacuation.ThrowFinished -= HandleThrowFinished;
        }

        private void RefreshInventory()
        {
            var slots = new InventoryEntry[InventoryLogic.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = _player.Items.Slot(i);
            }

            SetInventory(_player.Items.Hand, slots);
            SetDropHint(_player.Items.HeldTool != null && !_player.Carry.IsCarrying);
        }

        private void HandlePropChanged(ClutterItem prop)
        {
            if (prop == null)
            {
                RefreshInventory();
                return;
            }

            SetHeldProp(prop.Icon);
            SetDropHint(false);
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            if (resident == null)
            {
                RefreshInventory();
                return;
            }

            SetHeldProp(resident.Icon);
            SetDropHint(true);
            if (!_carryHintShown)
            {
                _carryHintShown = true;
                ShowToast(_texts[TextId.ToastCarryHint]);
            }
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

        private void HandleRescued(Rescuable resident, int rescued) => ShowRescued(rescued, _rescueTotal);

        private void HandleThrowFinished()
        {
            SetCrosshairVisible(true);
            ShowToast(_texts.Format(TextId.ToastRescuedCount, _evacuation.Rescued, _rescueTotal));
        }
    }
}
