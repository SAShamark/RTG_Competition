using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor2
{
    public sealed class EvacuateGrandmaStep : MissionStep
    {
        private readonly Floor2Setup _setup;

        public EvacuateGrandmaStep(Floor2Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioEvacuateGrandma];

        public override void Enter()
        {
            ShowToast(TextId.ToastDocumentsSaved);
            _setup.Grandma.SetCanBeCarried(true);
            _setup.Window.SetReady(true);
            SetObjective(TextId.ObjectiveEvacuateGrandma);
            Talk(new[]
            {
                LineToPlayer(DialogIds.GrandmaReady, _setup.Grandma.Head),
                PlayerLine(DialogIds.PlayerHoldOn, _setup.Grandma.Head),
            });

            _setup.Grandma.PickedUp += HandlePickedUp;
            Context.Evacuation.ResidentRescued += HandleRescued;
        }

        public override void Dispose()
        {
            _setup.Grandma.PickedUp -= HandlePickedUp;
            Context.Evacuation.ResidentRescued -= HandleRescued;
        }

        private void HandlePickedUp(Rescuable grandma) => GuideToWindow(_setup.Window);

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident == _setup.Grandma)
            {
                Complete();
            }
        }
    }
}
