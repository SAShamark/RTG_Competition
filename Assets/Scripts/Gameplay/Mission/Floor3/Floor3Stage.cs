using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Mission.Floor3
{
    public sealed class Floor3Stage : MissionStage
    {
        private readonly Floor3Setup _setup;
        private readonly ValveRoomController _valveRoom;
        private readonly WheelSearchController _wheelSearch;
        private BreakableDoor _flatDoorPlanks;
        private DoorKey _pantryKey;

        public Floor3Stage(Floor3Setup setup, StackItem flywheel, MissionContext context) : base(context)
        {
            _setup = setup;
            _valveRoom = new ValveRoomController(context.Screen, setup.PipeValves, () => context.Player.Items.RemoveOne(ItemType.Wheel), context.Texts);
            _wheelSearch = new WheelSearchController(context.Screen, new[] { flywheel, setup.Handle }, _valveRoom, context.Texts);
        }

        protected override void OnInit()
        {
            _setup.Neighbor.SetCanBeCarried(false);
            _setup.Window.SetReady(false);
            _setup.StoveFire.SetReignite(true);
            _setup.Valve.SetRemoteOnly(true);
            _setup.Fridge.enabled = false;
            _setup.FlatDoor.SetLocked(true);
            _flatDoorPlanks = _setup.FlatDoor.GetComponentInChildren<BreakableDoor>();
            _setup.PantryDoor.SetLocked(true);
            _setup.PantryDoor.SetLockedPrompt(Texts[TextId.PromptLockpick], "");
            _setup.PantryDoor.SetForceItem(ItemType.Axe);
            _pantryKey = _setup.PantryDoor.GetComponentInChildren<DoorKey>();

            _valveRoom.Init();
            _wheelSearch.Init();

            _pantryKey.Turned += HandlePantryKeyTurned;
            _setup.PantryDoor.Forced += HandlePantryForced;
            _valveRoom.Solved += _setup.Valve.ForceClose;
            _setup.Valve.Closed += HandleGasClosed;
            _setup.Handle.Taken += HandleHandleTaken;
            _flatDoorPlanks.Chopped += HandleFlatDoorChopped;
        }

        protected override void OnDispose()
        {
            _pantryKey.Turned -= HandlePantryKeyTurned;
            _setup.PantryDoor.Forced -= HandlePantryForced;
            _valveRoom.Solved -= _setup.Valve.ForceClose;
            _setup.Valve.Closed -= HandleGasClosed;
            _setup.Handle.Taken -= HandleHandleTaken;
            _flatDoorPlanks.Chopped -= HandleFlatDoorChopped;

            _wheelSearch.Dispose();
            _valveRoom.Dispose();
        }

        protected override void OnBegin() => _setup.Valve.StartHiss();

        protected override MissionStep[] CreateSteps() => new MissionStep[]
        {
            new FindNeighborStep(_setup, _flatDoorPlanks, Context),
            new CloseGasStep(_setup, _wheelSearch, Context),
            new EvacuateResidentStep(_setup.Neighbor, _setup.Window,
                new EvacuationBrief(TextId.ObjectiveEvacuateNeighbor, TextId.RadioEvacuateNeighbor, DialogIds.PlayerRescueNeighbor, DialogIds.NeighborIsItFree, false),
                Context, MovePartnerToSpot),
            new LeaveBuildingStep(Context),
        };

        protected override void OnFinished() => SetObjective(TextId.ObjectiveRun);

        private void MovePartnerToSpot() => _setup.Partner.SetPositionAndRotation(_setup.PartnerSpot.position, _setup.PartnerSpot.rotation);

        private void HandleFlatDoorChopped(BreakableDoor planks, Vector3 point) => Begin();

        private void HandleHandleTaken(ItemPickup handle) => ShowToast(TextId.ToastWheelFound);

        private void HandleGasClosed()
        {
            _setup.GasLeak.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _setup.StoveFire.SetReignite(false);
            Say(DialogIds.NeighborGasClosed);
        }

        private void HandlePantryKeyTurned(DoorKey key) => OpenPantry();

        private void HandlePantryForced(Door door)
        {
            _pantryKey.SetTurned();
            AudioService.Instance.PlayAt(SoundType.LockBreak, door.transform.position);
            OpenPantry();
        }

        private void OpenPantry()
        {
            _pantryKey.Turned -= HandlePantryKeyTurned;
            _setup.PantryDoor.Forced -= HandlePantryForced;
            _setup.PantryDoor.SetForceItem(ItemType.None);
            _setup.PantryDoor.Open(Context.Player.transform.position);
            ShowToast(TextId.ToastLockOpened);
        }
    }
}
