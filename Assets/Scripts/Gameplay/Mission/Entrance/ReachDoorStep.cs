using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Entrance
{
    public sealed class ReachDoorStep : MissionStep
    {
        private readonly EntranceSetup _setup;

        public ReachDoorStep(EntranceSetup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioEntranceStart];

        public override void Enter()
        {
            if (_setup.Axe.IsTaken)
            {
                Complete();
                return;
            }

            GuideTo(_setup.DoorPoint, TextId.NavEntrance);
            foreach (Door door in _setup.Doors)
            {
                door.LockedAttempt += HandleLockedAttempt;
            }

            _setup.Axe.Taken += HandleAxeTaken;
        }

        public override void Dispose()
        {
            foreach (Door door in _setup.Doors)
            {
                door.LockedAttempt -= HandleLockedAttempt;
            }

            _setup.Axe.Taken -= HandleAxeTaken;
        }

        private void HandleLockedAttempt(Door door) => Complete();

        private void HandleAxeTaken(ItemPickup axe) => Complete();
    }
}
