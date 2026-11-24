using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Mission.Floor2
{
    public sealed class FindGrandmaStep : MissionStep
    {
        private const float FirstCoughDelay = 1.5f;
        private const float CoughMinDelay = 6f;
        private const float CoughMaxDelay = 9f;

        private readonly Floor2Setup _setup;
        private readonly BreakableDoor _doorPlanks;
        private float _coughTimer;

        public FindGrandmaStep(Floor2Setup setup, BreakableDoor doorPlanks, MissionContext context) : base(context)
        {
            _setup = setup;
            _doorPlanks = doorPlanks;
        }

        public override string RadioHint => Texts[TextId.RadioGrandmaCough];

        private bool IsGrandmaReached => _setup.GrandmaDoor.IsOpen || _doorPlanks.IsChopped || _setup.Documents.IsTaken;

        public override void Enter()
        {
            if (IsGrandmaReached)
            {
                Complete();
                return;
            }

            SetObjective(TextId.ObjectiveFindFloor2Residents);
            _coughTimer = FirstCoughDelay;
            _setup.GrandmaDoor.Opened += HandleDoorOpened;
            _doorPlanks.Chopped += HandleDoorChopped;
            _setup.Documents.Taken += Complete;
        }

        public override void Dispose()
        {
            _setup.GrandmaDoor.Opened -= HandleDoorOpened;
            _doorPlanks.Chopped -= HandleDoorChopped;
            _setup.Documents.Taken -= Complete;
        }

        public override void Tick(float deltaTime)
        {
            if (Dialogue.IsPlaying)
            {
                return;
            }

            _coughTimer -= deltaTime;
            if (_coughTimer <= 0f)
            {
                AudioService.Instance.PlayAt(SoundType.GrandmaCough, _setup.Grandma.transform.position);
                _coughTimer = Random.Range(CoughMinDelay, CoughMaxDelay);
            }
        }

        private void HandleDoorOpened(Door door) => Complete();

        private void HandleDoorChopped(BreakableDoor planks, Vector3 point) => Complete();
    }
}
