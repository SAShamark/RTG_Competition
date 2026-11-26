using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;

namespace Gameplay.Mission.Core
{
    public readonly struct EvacuationBrief
    {
        public readonly TextId Objective;
        public readonly TextId RadioHint;
        public readonly string PlayerLineId;
        public readonly string ResidentLineId;
        public readonly bool ResidentSpeaksFirst;

        public EvacuationBrief(TextId objective, TextId radioHint, string playerLineId, string residentLineId, bool residentSpeaksFirst)
        {
            Objective = objective;
            RadioHint = radioHint;
            PlayerLineId = playerLineId;
            ResidentLineId = residentLineId;
            ResidentSpeaksFirst = residentSpeaksFirst;
        }
    }

    public sealed class EvacuateResidentStep : MissionStep
    {
        private readonly Rescuable _resident;
        private readonly EvacuationWindow _window;
        private readonly EvacuationBrief _brief;
        private readonly Action _prepare;

        public EvacuateResidentStep(Rescuable resident, EvacuationWindow window, EvacuationBrief brief, MissionContext context,
            Action prepare) : base(context)
        {
            _resident = resident;
            _window = window;
            _brief = brief;
            _prepare = prepare;
        }

        public override string RadioHint => Texts[_brief.RadioHint];

        public override void Enter()
        {
            _prepare();
            _resident.SetCanBeCarried(true);
            _window.SetReady(true);
            SetObjective(_brief.Objective);

            DialogueLine playerLine = PlayerLine(_brief.PlayerLineId, _resident.Character);
            DialogueLine residentLine = LineToPlayer(_brief.ResidentLineId, _resident.Character);
            Talk(_brief.ResidentSpeaksFirst ? new[] { residentLine, playerLine } : new[] { playerLine, residentLine });

            _resident.PickedUp += HandlePickedUp;
            Context.Evacuation.ResidentRescued += HandleRescued;
        }

        public override void Dispose()
        {
            _resident.PickedUp -= HandlePickedUp;
            Context.Evacuation.ResidentRescued -= HandleRescued;
        }

        private void HandlePickedUp(Rescuable resident) => GuideToWindow(_window);

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident == _resident)
            {
                Complete();
            }
        }
    }
}
