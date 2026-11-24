using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Entrance
{
    public sealed class EntranceStage : MissionStage
    {
        private readonly EntranceSetup _setup;

        public EntranceStage(EntranceSetup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint
        {
            get
            {
                if (IsFinished)
                {
                    return Texts[TextId.RadioSearchFloors];
                }

                return IsStarted ? base.RadioHint : Texts[TextId.RadioEntranceStart];
            }
        }

        protected override void OnInit()
        {
            foreach (Door door in _setup.Doors)
            {
                door.SetLocked(true);
                door.SetForceItem(ItemType.Axe);
            }
        }

        protected override MissionStep[] CreateSteps() => new MissionStep[]
        {
            new ReachDoorStep(_setup, Context),
            new FindAxeStep(_setup, Context),
            new ForceDoorStep(_setup, Context),
            new BreakInStep(_setup, Context),
        };

        protected override void OnFinished() => SetObjective(TextId.ObjectiveRescueResidents);
    }
}
