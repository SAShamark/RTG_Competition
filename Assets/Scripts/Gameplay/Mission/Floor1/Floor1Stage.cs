using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor1
{
    public sealed class Floor1Stage : MissionStage
    {
        private readonly Floor1Setup _setup;

        public Floor1Stage(Floor1Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        protected override void OnInit()
        {
            LockDoor(_setup.KnockDoor, TextId.HintLocked);
            foreach (Door door in _setup.EmptyFlatDoors)
            {
                LockDoor(door, TextId.HintFlatEmpty);
            }

            _setup.ElevatorButton.Pressed += HandleElevatorPressed;
        }

        protected override void OnDispose() => _setup.ElevatorButton.Pressed -= HandleElevatorPressed;

        protected override MissionStep[] CreateSteps() => new MissionStep[]
        {
            new TakeExtinguisherStep(Context),
            new PutOutStairsStep(_setup, Context),
        };

        protected override void OnFinished()
        {
            SetObjective(TextId.ObjectiveGoToFloor2);
            Say(DialogIds.PartnerStairsClear);
        }

        private void LockDoor(Door door, TextId hint)
        {
            door.SetLocked(true);
            door.SetLockedPrompt(Texts[TextId.PromptOpen], Texts[hint]);
        }

        private void HandleElevatorPressed(ElevatorButton button) => Say(DialogIds.PartnerNoElevator);
    }
}
