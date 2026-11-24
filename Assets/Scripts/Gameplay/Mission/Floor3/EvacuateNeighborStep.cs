using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor3
{
    public sealed class EvacuateNeighborStep : MissionStep
    {
        private readonly Floor3Setup _setup;

        public EvacuateNeighborStep(Floor3Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioEvacuateNeighbor];

        public override void Enter()
        {
            _setup.Neighbor.SetCanBeCarried(true);
            _setup.Window.SetReady(true);
            _setup.Partner.SetPositionAndRotation(_setup.PartnerSpot.position, _setup.PartnerSpot.rotation);
            SetObjective(TextId.ObjectiveEvacuateNeighbor);
            Talk(new[]
            {
                PlayerLine(DialogIds.PlayerRescueNeighbor, _setup.Neighbor.Head),
                LineToPlayer(DialogIds.NeighborIsItFree, _setup.Neighbor.Head),
            });

            _setup.Neighbor.PickedUp += HandlePickedUp;
            Context.Evacuation.ResidentRescued += HandleRescued;
        }

        public override void Dispose()
        {
            _setup.Neighbor.PickedUp -= HandlePickedUp;
            Context.Evacuation.ResidentRescued -= HandleRescued;
        }

        private void HandlePickedUp(Rescuable neighbor) => GuideToWindow(_setup.Window);

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident == _setup.Neighbor)
            {
                Complete();
            }
        }
    }
}
