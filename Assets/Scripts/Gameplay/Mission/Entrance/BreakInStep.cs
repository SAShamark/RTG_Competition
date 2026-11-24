using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Entrance
{
    public sealed class BreakInStep : MissionStep
    {
        private readonly EntranceSetup _setup;
        private float _delay;

        public BreakInStep(EntranceSetup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioForceDoor];

        public override void Enter() => _delay = _setup.CutsceneDelay;

        public override void Tick(float deltaTime)
        {
            _delay -= deltaTime;
            if (_delay > 0f)
            {
                return;
            }

            foreach (Door door in _setup.Doors)
            {
                door.Open(_setup.InsidePoint.position);
            }

            Complete();
        }
    }
}
