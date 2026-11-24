using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor1
{
    public sealed class PutOutStairsStep : MissionStep
    {
        private readonly Floor1Setup _setup;

        public PutOutStairsStep(Floor1Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioPutOutStairs];

        public override void Enter()
        {
            if (_setup.AreFiresOut)
            {
                Complete();
                return;
            }

            SetObjective(TextId.ObjectivePutOutStairs);
            foreach (FireSource fire in _setup.Fires)
            {
                fire.Extinguished += HandleFireOut;
            }
        }

        public override void Dispose()
        {
            foreach (FireSource fire in _setup.Fires)
            {
                fire.Extinguished -= HandleFireOut;
            }
        }

        private void HandleFireOut(FireSource fire)
        {
            if (_setup.AreFiresOut)
            {
                Complete();
            }
        }
    }
}
