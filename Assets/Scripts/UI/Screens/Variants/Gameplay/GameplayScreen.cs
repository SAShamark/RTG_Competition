using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UI.Screens.Variants.Cutscene.Widgets;
using UI.Screens.Variants.Gameplay.Widgets;
using UI.Screens.Base;
using UnityEngine;
using UnityEngine.Serialization;
using Vr;

namespace UI.Screens.Variants.Gameplay
{
    public sealed class GameplayScreen : BaseScreen
    {
        [Header("Widgets")]
        [SerializeField] private RescueNoticeWidget _rescued;
        [SerializeField] private InteractionPromptWidget _prompt;
        [FormerlySerializedAs("_partner")]
        [SerializeField] private DialogWidget _dialog;
        [SerializeField] private NavMarkerWidget _navMarker;
        [SerializeField] private ToastWidget _toasts;
        [SerializeField] private HeatVignetteWidget _heat;
        [SerializeField] private PressureGaugeWidget _pressure;
        [SerializeField] private FireProgressWidget _fireProgress;

        private FirstPersonController _player;
        private VrWristDisplay _wrist;
        private FireHeatLogic _fireHeat;
        private TextLibrary _texts;
        private bool _extinguisherHintShown;
        private bool _carryHintShown;
        private ClutterItem _heldProp;
        private Rescuable _carriedResident;

        public NavMarkerWidget NavMarker => _navMarker;

        public void BindWrist(VrWristDisplay wrist) => _wrist = wrist;

        public void SetObjective(string text) => _wrist.SetObjective(text);

        public void UpdateObjectiveText(string text) => _wrist.UpdateObjective(text);

        public void SetHudVisible(bool isVisible) => _safeAreaFitter.gameObject.SetActive(isVisible);

        public void ShowRescued(int value, int total)
        {
            _rescued.Show(value, total);
            _wrist.ShowRescued(value, total);
        }

        public void HideRescued()
        {
            _rescued.Hide();
            _wrist.HideRescued();
        }



        public void SetHeat(float intensity01) => _heat.SetIntensity(intensity01);

        public void ShowFireProgress(float value01)
        {
            _fireProgress.gameObject.SetActive(true);
            _fireProgress.Snap(value01);
        }

        public void SetFireProgress(float value01) => _fireProgress.SetProgress(value01);

        public void HideFireProgress() => _fireProgress.gameObject.SetActive(false);

        public void SetInteractionHint(string hint) => _prompt.Show(hint);

        public void Say(DialogPhrase phrase) => _dialog.Say(phrase);

        public bool IsDialogBusy => _dialog.IsBusy;

        public void ClearDialog() => _dialog.Clear();

        public void ShowToast(string message) => _toasts.Show(message);

        public void Init(FirstPersonController player, FireHeatLogic fireHeat, TextLibrary texts)
        {
            _player = player;
            _fireHeat = fireHeat;
            _pressure.Init(player.Extinguisher, player.Axe);
            _texts = texts;
            HideRescued();
            SetHeat(0f);
            HideFireProgress();
            RefreshInventory();

            _player.Interaction.HintChanged += SetInteractionHint;
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
