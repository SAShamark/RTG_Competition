using Gameplay.Dialogue;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor3
{
    public sealed class LeaveBuildingStep : MissionStep
    {
        private const float BurstDelay = 1.2f;

        private float _delay;

        public LeaveBuildingStep(MissionContext context) : base(context)
        {
        }

        public override string RadioHint => Texts[TextId.RadioSearchFloors];

        public override void Enter()
        {
            Navigation.Clear();
            SetObjective(TextId.ObjectiveLeaveBuilding);
            Say(DialogIds.PartnerNeighborReceived);
            _delay = BurstDelay;
        }

        public override void Tick(float deltaTime)
        {
            _delay -= deltaTime;
            if (_delay <= 0f)
            {
                Complete();
            }
        }
    }
}
