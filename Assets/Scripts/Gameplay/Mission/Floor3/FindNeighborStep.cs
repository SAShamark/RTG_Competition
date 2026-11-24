using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;
using UnityEngine;

namespace Gameplay.Mission.Floor3
{
    public sealed class FindNeighborStep : MissionStep
    {
        private readonly Floor3Setup _setup;
        private readonly BreakableDoor _flatDoorPlanks;

        public FindNeighborStep(Floor3Setup setup, BreakableDoor flatDoorPlanks, MissionContext context) : base(context)
        {
            _setup = setup;
            _flatDoorPlanks = flatDoorPlanks;
        }

        public override string RadioHint => Texts[TextId.RadioNeighborScream];

        public override void Enter()
        {
            SetObjective(TextId.ObjectiveFindNeighbor);
            Say(DialogIds.NeighborScream);
            Guide(_setup.RouteToFlat, _setup.FlatZone, TextId.NavScream);
            if (_flatDoorPlanks.IsChopped)
            {
                Complete();
                return;
            }

            _flatDoorPlanks.Chopped += HandleChopped;
        }

        public override void Dispose() => _flatDoorPlanks.Chopped -= HandleChopped;

        public override void Tick(float deltaTime)
        {
            if (_setup.FlatZone.bounds.Contains(PlayerHead.position))
            {
                Complete();
            }
        }

        private void HandleChopped(BreakableDoor planks, Vector3 point) => Complete();
    }
}
