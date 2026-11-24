using Gameplay.Dialogue;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;
using UI.Popups;
using UnityEngine;

namespace Gameplay.Mission.Floor2
{
    public sealed class Floor2Stage : MissionStage
    {
        private readonly Floor2Setup _setup;
        private BreakableDoor _doorPlanks;

        public Floor2Stage(Floor2Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public void ShowRouteToFlat()
        {
            if (!IsFinished && _setup.RouteToFlat.Length > 0)
            {
                Guide(_setup.RouteToFlat, _setup.FlatZone, TextId.NavGrandma);
            }
        }

        protected override void OnInit()
        {
            _setup.Grandma.SetCanBeCarried(false);
            _setup.Window.SetReady(false);
            _setup.GrandmaDoor.SetLocked(true);
            _doorPlanks = _setup.GrandmaDoor.GetComponentInChildren<BreakableDoor>();

            _setup.ArrivalZone.Entered += HandleArrived;
            _setup.GrandmaDoor.Opened += HandleDoorOpened;
            _doorPlanks.Chopped += HandleDoorChopped;
            _setup.DoorFire.Extinguished += HandleDoorFireOut;
            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved += HandlePaintingMoved;
            }

            _setup.Safe.OpenRequested += HandleSafeOpenRequested;
            _setup.Safe.Opened += HandleSafeOpened;
        }

        protected override void OnDispose()
        {
            _setup.ArrivalZone.Entered -= HandleArrived;
            _setup.GrandmaDoor.Opened -= HandleDoorOpened;
            _doorPlanks.Chopped -= HandleDoorChopped;
            _setup.DoorFire.Extinguished -= HandleDoorFireOut;
            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved -= HandlePaintingMoved;
            }

            _setup.Safe.OpenRequested -= HandleSafeOpenRequested;
            _setup.Safe.Opened -= HandleSafeOpened;
        }

        protected override MissionStep[] CreateSteps() => new MissionStep[]
        {
            new FindGrandmaStep(_setup, _doorPlanks, Context),
            new ReachGrandmaStep(_setup, Context),
            new GetDocumentsStep(_setup, Context),
            new EvacuateGrandmaStep(_setup, Context),
        };

        protected override void OnFinished()
        {
            SetObjective(TextId.ObjectiveGoToFloor3);
            if (_setup.RouteUp.Length > 0)
            {
                Guide(_setup.RouteUp, null, TextId.NavFloor3);
            }

            Say(DialogIds.PartnerGrandmaReceived);
        }

        private void HandleArrived(TriggerZone zone) => Begin();

        private void HandleDoorOpened(Door door) => Begin();

        private void HandleDoorChopped(BreakableDoor planks, Vector3 point) => Begin();

        private void HandleDoorFireOut(FireSource fire) => Begin();

        private void HandlePaintingMoved(WallPainting painting) => Begin();

        private void HandleSafeOpenRequested(WallSafe safe) => Context.Puzzles.Present(PopupTypes.SafePuzzle, safe.Open);

        private void HandleSafeOpened(WallSafe safe)
        {
            ShowToast(TextId.ToastSafeOpened);
            Say(DialogIds.GrandmaThanks, _setup.Grandma.Head);
        }
    }
}
