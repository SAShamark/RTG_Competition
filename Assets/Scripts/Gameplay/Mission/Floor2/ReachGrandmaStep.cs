using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor2
{
    public sealed class ReachGrandmaStep : MissionStep
    {
        private readonly Floor2Setup _setup;

        public ReachGrandmaStep(Floor2Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioReachGrandma];

        public override void Enter()
        {
            if (_setup.Documents.IsTaken)
            {
                Complete();
                return;
            }

            Say(DialogIds.GrandmaInBedroom, _setup.Grandma.Head);
            RefreshObjective();
            _setup.DoorFire.Extinguished += HandleDoorFireOut;
            _setup.Grandma.Refused += HandleRefused;
            _setup.Documents.Taken += Complete;
        }

        public override void Dispose()
        {
            _setup.DoorFire.Extinguished -= HandleDoorFireOut;
            _setup.Grandma.Refused -= HandleRefused;
            _setup.Documents.Taken -= Complete;
        }

        private void RefreshObjective() =>
            SetObjective(_setup.DoorFire.IsOut ? TextId.ObjectiveLeadGrandma : TextId.ObjectiveReachGrandma);

        private void HandleDoorFireOut(FireSource fire) => RefreshObjective();

        private void HandleRefused(Rescuable grandma) => Complete();
    }
}
