using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Residents;
using Gameplay.Mission;
using System;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission.Floor3
{
    public sealed class Floor3Controller : StageController
    {
        private const float BurstDelay = 1.2f;
        private const float GasHintSprayTime = 1f;

        private enum Step { Waiting, FindNeighbor, Kitchen, Evacuate, Done }


        private Step _step = Step.Waiting;
        private bool _seenNeighbor;
        private bool _gasHinted;
        private float _stoveSprayTime;
        private bool _hasHandle;
        private bool _gasClosed;
        private float _burstTimer = -1f;

        public override bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public override string RadioHint
        {
            get
            {
                if (_step == Step.FindNeighbor)
                {
                    return _texts[TextId.RadioNeighborScream];
                }

                if (_step == Step.Kitchen)
                {
                    if (!_gasClosed && !_hasHandle)
                    {
                        return _texts[TextId.RadioPantry];
                    }

                    if (!_gasClosed)
                    {
                        return _texts[TextId.RadioValves];
                    }

                    return _texts[TextId.RadioGasClosed];
                }

                if (_step == Step.Evacuate)
                {
                    return _texts[TextId.RadioEvacuateNeighbor];
                }

                return _texts[TextId.RadioGoToFloor4];
            }
        }
        public event Action WheelsNeeded;


        private readonly Floor3Setup _setup;
        private readonly GameplayScreen _screen;
        private readonly DialogueDirector _dialogue;
        private readonly Transform _playerHead;
        private readonly ExtinguisherLogic _extinguisher;
        private readonly NavigationGuide _nav;
        private readonly EvacuationController _evacuation;
        private readonly TextLibrary _texts;

        private BreakableDoor _flatDoorPlanks;

        public Floor3Controller(Floor3Setup setup, MissionContext context)
        {
            _setup = setup;
            _screen = context.Screen;
            _dialogue = context.Dialogue;
            _playerHead = context.Player.Head;
            _extinguisher = context.Player.Extinguisher;
            _nav = context.Navigation;
            _evacuation = context.Evacuation;
            _texts = context.Texts;
        }

        public override void Init()
        {
            _setup.Neighbor.SetCanBeCarried(false);
            _setup.Window.SetReady(false);
            _setup.StoveFire.SetReignite(true);

            _extinguisher.FireHit += HandleFireHit;
            _setup.StoveFire.Extinguished += HandleFireOut;
            _setup.Valve.SetRemoteOnly(true);
            _setup.Valve.Closed += HandleGasClosed;
            _setup.Handle.Taken += HandleHandleTaken;
            _setup.Neighbor.Refused += HandleNeighborRefused;
            _setup.Neighbor.PickedUp += HandleResidentPickedUp;
            _evacuation.ResidentRescued += HandleRescued;
            _setup.FlatDoor.SetLocked(true);
            _flatDoorPlanks = _setup.FlatDoor.GetComponentInChildren<BreakableDoor>();
            _flatDoorPlanks.Chopped += HandleFlatDoorChopped;
        }

        public override void Dispose()
        {
            _extinguisher.FireHit -= HandleFireHit;
            _setup.StoveFire.Extinguished -= HandleFireOut;
            _setup.Valve.Closed -= HandleGasClosed;
            _setup.Handle.Taken -= HandleHandleTaken;
            _setup.Neighbor.Refused -= HandleNeighborRefused;
            _setup.Neighbor.PickedUp -= HandleResidentPickedUp;
            _evacuation.ResidentRescued -= HandleRescued;
            _flatDoorPlanks.Chopped -= HandleFlatDoorChopped;
        }

        private void HandleFlatDoorChopped(BreakableDoor planks, Vector3 point)
        {
            Begin();
            MeetNeighbor();
        }

        public void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            _step = Step.FindNeighbor;
            _setup.Valve.StartHiss();
            _screen.SetObjective(_texts[TextId.ObjectiveFindNeighbor]);
            _screen.Say(_dialogue.Library.Get("N01"));
            _nav.SetRoute(_setup.RouteToFlat, _setup.FlatZone, _texts[TextId.NavScream]);
        }

        public override void Tick(float deltaTime)
        {
            if (_step == Step.FindNeighbor && _setup.FlatZone.bounds.Contains(_playerHead.position))
            {
                MeetNeighbor();
            }
            TickBlast(deltaTime);
        }

        private void TickBlast(float deltaTime)
        {
            if (_burstTimer <= 0f)
            {
                return;
            }

            _burstTimer -= deltaTime;
            if (_burstTimer <= 0f)
            {
                _screen.SetObjective(_texts[TextId.ObjectiveRun]);
                Finish();
            }
        }

        private void MeetNeighbor()
        {
            if (_seenNeighbor)
            {
                return;
            }

            _seenNeighbor = true;
            _step = Step.Kitchen;
            _screen.SetObjective(_texts[TextId.ObjectiveReachNeighbor]);
            _screen.Say(_dialogue.Library.Get("N02"), _setup.Neighbor.Head);
        }

        private void ShowPantryRoute()
        {
            if (_gasClosed)
            {
                return;
            }

            _screen.SetObjective(_texts[TextId.ObjectiveCloseGas]);
            WheelsNeeded?.Invoke();
            _nav.SetRoute(_setup.RouteToPantry, _setup.PantryZone, _texts[TextId.NavPantry]);
        }

        private void HandleFireHit(FireSource fire)
        {
            if (fire != _setup.StoveFire || _gasClosed || _gasHinted)
            {
                return;
            }

            _stoveSprayTime += Time.deltaTime;
            if (_stoveSprayTime < GasHintSprayTime)
            {
                return;
            }

            _gasHinted = true;
            _screen.ShowToast(_texts[TextId.ToastGasFeedsFire]);
            Talk(new[]
            {
                _dialogue.Line("M05", _playerHead, _setup.Neighbor.Head),
                _dialogue.Line("N03", _setup.Neighbor.Head, _playerHead),
            }, ShowPantryRoute);
        }

        private void HandleHandleTaken(ItemPickup handle)
        {
            _hasHandle = true;
            _screen.ShowToast(_texts[TextId.ToastWheelFound]);
        }

        private void HandleGasClosed()
        {
            _gasClosed = true;
            _setup.GasLeak.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _screen.Say(_dialogue.Library.Get("N04"));
            _setup.StoveFire.SetReignite(false);
            _screen.SetObjective(_setup.StoveFire.IsOut ? _texts[TextId.ObjectiveEvacuateNeighbor] : _texts[TextId.ObjectivePutOutKitchen]);
            if (!_setup.StoveFire.IsOut)
            {
                _nav.SetRoute(_setup.RouteToKitchen, _setup.KitchenZone, _texts[TextId.NavKitchen]);
            }
            else
            {
                _nav.Clear();
            }
            if (_setup.StoveFire.IsOut)
            {
                StartEvacuation();
            }
        }

        private void HandleFireOut(FireSource fire)
        {
            if (_gasClosed)
            {
                StartEvacuation();
            }
        }

        private void StartEvacuation()
        {
            if (_step == Step.Evacuate || _step == Step.Done)
            {
                return;
            }

            _step = Step.Evacuate;
            _setup.Neighbor.SetCanBeCarried(true);
            _setup.Window.SetReady(true);
            _setup.Partner.SetPositionAndRotation(_setup.PartnerSpot.position, _setup.PartnerSpot.rotation);

            _screen.SetObjective(_texts[TextId.ObjectiveEvacuateNeighbor]);
            Talk(new[]
            {
                _dialogue.Line("M06", _playerHead, _setup.Neighbor.Head),
                _dialogue.Line("N05", _setup.Neighbor.Head, _playerHead),
            });
        }

        private void HandleNeighborRefused(Rescuable neighbor)
        {
        }

        private void HandleResidentPickedUp(Rescuable resident) =>
            _nav.SetRoute(new[] { _setup.Window.transform }, null, _texts[TextId.NavWindow]);

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident != _setup.Neighbor)
            {
                return;
            }

            _step = Step.Done;
            _nav.Clear();
            _screen.SetObjective(_texts[TextId.ObjectiveLeaveBuilding]);
            _screen.Say(_dialogue.Library.Get("P09"));
            _burstTimer = BurstDelay;
        }


        private void Talk(params DialogueLine[] lines) => Talk(lines, null);

        private void Talk(DialogueLine[] lines, Action onFinished)
        {
            _dialogue.Play(lines, onFinished);
        }
    }
}
