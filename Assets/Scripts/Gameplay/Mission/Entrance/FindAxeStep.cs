using Gameplay.Dialogue;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Entrance
{
    public sealed class FindAxeStep : MissionStep
    {
        private readonly EntranceSetup _setup;

        public FindAxeStep(EntranceSetup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioFindAxe];

        public override void Enter()
        {
            if (_setup.Axe.IsTaken)
            {
                Complete();
                return;
            }

            SetObjective(TextId.ObjectiveFindAxe);
            Say(DialogIds.PartnerEntranceLocked);
            GuideTo(_setup.LockerPoint, TextId.NavTruck);
            _setup.Axe.Taken += HandleAxeTaken;
        }

        public override void Dispose() => _setup.Axe.Taken -= HandleAxeTaken;

        private void HandleAxeTaken(ItemPickup axe) => Complete();
    }
}
