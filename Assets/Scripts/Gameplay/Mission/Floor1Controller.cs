using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission
{
    public sealed class Floor1Controller : StageController
    {
        private enum Step { Waiting, OpenCabinet, PutOut, Done }


        private Step _step = Step.Waiting;

        public override bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public override string RadioHint => _step switch
        {
            Step.OpenCabinet => _texts[TextId.RadioOpenCabinet],
            Step.PutOut => _texts[TextId.RadioPutOutStairs],
            _ => "",
        };



        private readonly Floor1Setup _setup;
        private readonly GameplayScreen _screen;
        private readonly InventoryLogic _items;
        private readonly DialogLibrary _dialogs;
        private readonly TextLibrary _texts;

        public Floor1Controller(Floor1Setup setup, MissionContext context)
        {
            _setup = setup;
            _screen = context.Screen;
            _items = context.Player.Items;
            _dialogs = context.Dialogue.Library;
            _texts = context.Texts;
        }

        public override void Init()
        {
            _setup.KnockDoor.SetLocked(true);
            _setup.KnockDoor.SetLockedPrompt(_texts[TextId.PromptOpen], _texts[TextId.HintLocked]);
            foreach (Door door in _setup.EmptyFlatDoors)
            {
                door.SetLocked(true);
                door.SetLockedPrompt(_texts[TextId.PromptOpen], _texts[TextId.HintFlatEmpty]);
            }

            _items.InventoryChanged += HandleInventoryChanged;
            _setup.Cabinet.Opened += HandleCabinetOpened;
            _setup.ElevatorButton.Pressed += HandleElevatorPressed;

            foreach (FireSource fire in _setup.Fires)
            {
                fire.Extinguished += HandleFireOut;
            }
        }

        public override void Dispose()
        {
            _items.InventoryChanged -= HandleInventoryChanged;
            _setup.Cabinet.Opened -= HandleCabinetOpened;
            _setup.ElevatorButton.Pressed -= HandleElevatorPressed;

            foreach (FireSource fire in _setup.Fires)
            {
                fire.Extinguished -= HandleFireOut;
            }
        }

        private void HandleElevatorPressed(ElevatorButton button) => _screen.Say(_dialogs.Get("P06"));

        public void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            if (AllOut())
            {
                _step = Step.Done;
                return;
            }

            bool hasExtinguisher = _items.Owns(ItemType.Extinguisher);
            _step = hasExtinguisher ? Step.PutOut : Step.OpenCabinet;
            _screen.SetObjective(hasExtinguisher ? _texts[TextId.ObjectivePutOutStairs] : _texts[TextId.ObjectiveTakeExtinguisher]);
        }

        private void HandleCabinetOpened(Hatch cabinet)
        {
        }

        private void HandleInventoryChanged()
        {
            if (!_items.Owns(ItemType.Extinguisher) || _step == Step.Done || AllOut())
            {
                return;
            }

            if (_step == Step.OpenCabinet)
            {
                _step = Step.PutOut;
                _screen.SetObjective(_texts[TextId.ObjectivePutOutStairs]);
                _screen.Say(_dialogs.Get("P05"));
            }
        }

        private void HandleFireOut(FireSource fire)
        {
            if (_step == Step.Done || !AllOut())
            {
                return;
            }

            _step = Step.Done;
            _screen.SetObjective(_texts[TextId.ObjectiveGoToFloor2]);
            _screen.Say(_dialogs.Get("P07"));
            Finish();
        }

        private bool AllOut()
        {
            foreach (FireSource fire in _setup.Fires)
            {
                if (!fire.IsOut)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
