using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Entrance
{
    public sealed class ForceDoorStep : MissionStep
    {
        private readonly EntranceSetup _setup;

        public ForceDoorStep(EntranceSetup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[TextId.RadioForceDoor];

        public override void Enter()
        {
            SetObjective(TextId.ObjectiveForceDoor);
            ShowToast(TextId.ToastAxeTaken);
            GuideTo(_setup.DoorPoint, TextId.NavEntrance);
            foreach (Door door in _setup.Doors)
            {
                door.Forced += HandleForced;
            }
        }

        public override void Dispose()
        {
            foreach (Door door in _setup.Doors)
            {
                door.Forced -= HandleForced;
            }
        }

        private void HandleForced(Door door)
        {
            Navigation.Clear();
            _setup.ChainLock.Break((door.transform.position - _setup.InsidePoint.position).normalized);
            ShowToast(TextId.ToastLockBroken);
            Complete();
        }
    }
}
