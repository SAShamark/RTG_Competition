using Gameplay.Dialogue;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Residents;
using Services.Audio;
using System;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission
{
    public sealed class Floor2Controller : StageController
    {
        private const float CoughMinDelay = 6f;
        private const float CoughMaxDelay = 9f;
        private const float FirstCoughDelay = 1.5f;

        private enum Step { Waiting, FindGrandma, TalkToGrandma, GetDocuments, Evacuate, Done }

        private bool _safeFound;
        private bool _fireOut;

        private Step _step = Step.Waiting;
        private bool _sawGrandma;
        private bool _explainedDocuments;
        private int _refusals;
        private float _coughTimer = 2f;

        public override bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public override string RadioHint => _step switch
        {
            Step.FindGrandma => _texts[TextId.RadioGrandmaCough],
            Step.TalkToGrandma => _texts[TextId.RadioReachGrandma],
            Step.GetDocuments => _safeFound
                ? _texts[TextId.RadioOpenSafe]
                : _texts[TextId.RadioFindSafe],
            Step.Evacuate => _texts[TextId.RadioEvacuateGrandma],
            _ => _texts[TextId.RadioGoToFloor3],
        };
        public event Action<WallSafe> SafePuzzleRequested;


        private readonly Floor2Setup _setup;
        private readonly GameplayScreen _screen;
        private readonly DialogueDirector _dialogue;
        private readonly Transform _playerHead;
        private readonly EvacuationController _evacuation;
        private readonly NavigationGuide _nav;
        private readonly TextLibrary _texts;

        private BreakableDoor _grandmaDoorPlanks;

        public Floor2Controller(Floor2Setup setup, MissionContext context)
        {
            _setup = setup;
            _screen = context.Screen;
            _dialogue = context.Dialogue;
            _playerHead = context.Player.Head;
            _evacuation = context.Evacuation;
            _nav = context.Navigation;
            _texts = context.Texts;
        }

        public override void Init()
        {
            _setup.Grandma.SetCanBeCarried(false);
            _setup.Window.SetReady(false);

            _setup.ArrivalZone.Entered += HandleArrived;

            _setup.GrandmaDoor.SetLocked(true);
            _setup.GrandmaDoor.Opened += HandleDoorOpened;
            _grandmaDoorPlanks = _setup.GrandmaDoor.GetComponentInChildren<BreakableDoor>();
            _grandmaDoorPlanks.Chopped += HandleDoorChopped;

            _setup.DoorFire.Extinguished += HandleDoorFireOut;

            _setup.Grandma.Refused += HandleGrandmaRefused;
            _setup.Grandma.PickedUp += HandleResidentPickedUp;
            _setup.Safe.OpenRequested += HandleSafeOpenRequested;
            _setup.Safe.Opened += HandleSafeOpened;
            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved += HandlePaintingMoved;
            }

            _setup.Documents.Taken += HandleDocumentsTaken;
            _evacuation.ResidentRescued += HandleRescued;
        }

        public override void Dispose()
        {
            _setup.ArrivalZone.Entered -= HandleArrived;

            _setup.GrandmaDoor.Opened -= HandleDoorOpened;
            _grandmaDoorPlanks.Chopped -= HandleDoorChopped;

            _setup.DoorFire.Extinguished -= HandleDoorFireOut;

            _setup.Grandma.Refused -= HandleGrandmaRefused;
            _setup.Grandma.PickedUp -= HandleResidentPickedUp;
            _setup.Safe.OpenRequested -= HandleSafeOpenRequested;
            _setup.Safe.Opened -= HandleSafeOpened;
            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved -= HandlePaintingMoved;
            }

            _setup.Documents.Taken -= HandleDocumentsTaken;
            _evacuation.ResidentRescued -= HandleRescued;
        }

        public override void Tick(float deltaTime)
        {
            TickCough(deltaTime);
        }

        public void ShowRouteToFlat()
        {
            if (_step != Step.Done && _setup.RouteToFlat.Length > 0)
            {
                _nav.SetRoute(_setup.RouteToFlat, _setup.FlatZone, _texts[TextId.NavGrandma]);
            }
        }

        private void TickCough(float deltaTime)
        {
            if (_step != Step.FindGrandma || _dialogue.IsPlaying)
            {
                return;
            }

            _coughTimer -= deltaTime;
            if (_coughTimer <= 0f)
            {
                AudioService.Instance.PlayAt(SoundType.GrandmaCough, _setup.Grandma.transform.position);
                _coughTimer = UnityEngine.Random.Range(CoughMinDelay, CoughMaxDelay);
            }
        }

        private void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            _step = Step.FindGrandma;
            _coughTimer = FirstCoughDelay;
            _screen.SetObjective(_texts[TextId.ObjectiveFindFloor2Residents]);
        }

        private void HandleArrived(TriggerZone zone) => Begin();

        private void HandleDoorChopped(BreakableDoor planks, Vector3 point) => HandleDoorOpened(_setup.GrandmaDoor);

        private void HandleDoorOpened(Door door)
        {
            Begin();
            if (_sawGrandma)
            {
                return;
            }

            _sawGrandma = true;
            _step = Step.TalkToGrandma;
            _screen.SetObjective(_texts[TextId.ObjectiveReachGrandma]);
            _screen.Say(_dialogue.Library.Get("G01"), _setup.Grandma.Head);
        }

        private void HandleDoorFireOut(FireSource fire)
        {
            Begin();
            _fireOut = true;
            if (_step == Step.FindGrandma || _step == Step.TalkToGrandma)
            {
                _step = Step.TalkToGrandma;
                _screen.SetObjective(_texts[TextId.ObjectiveLeadGrandma]);
            }
        }

        private void HandleGrandmaRefused(Rescuable grandma)
        {
            Begin();
            if (!_explainedDocuments)
            {
                _explainedDocuments = true;
                _step = Step.GetDocuments;
                _screen.SetObjective(_safeFound ? _texts[TextId.ObjectiveOpenSafe] : _texts[TextId.ObjectiveFindDocuments]);
                Talk(
                    _dialogue.Line("M01", _playerHead, _setup.Grandma.Head),
                    _dialogue.Line("G02", _setup.Grandma.Head, _playerHead),
                    _dialogue.Line("M02", _playerHead, _setup.Grandma.Head),
                    _dialogue.Line("G03", _setup.Grandma.Head, _playerHead),
                    _dialogue.Line("M03", _playerHead, _setup.Grandma.Head),
                    _dialogue.Line("G04", _setup.Grandma.Head, _playerHead));
                return;
            }

            if (_step == Step.GetDocuments)
            {
                Bubble(++_refusals % 2 == 1 ? "G07" : _safeFound ? "G09" : "G08");
            }
        }

        private void HandlePaintingMoved(WallPainting painting)
        {
            Begin();
            if (!painting.HidesSafe)
            {
                return;
            }

            _safeFound = true;
            if (_step == Step.GetDocuments)
            {
                _screen.SetObjective(_texts[TextId.ObjectiveOpenSafe]);
            }
        }

        private void HandleSafeOpenRequested(WallSafe safe)
        {
            SafePuzzleRequested?.Invoke(safe);
        }

        private void HandleSafeOpened(WallSafe safe)
        {
            _screen.ShowToast(_texts[TextId.ToastSafeOpened]);
            if (_step == Step.GetDocuments)
            {
                _screen.SetObjective(_texts[TextId.ObjectiveTakeDocuments]);
            }

            Bubble("G05");
        }

        private void HandleDocumentsTaken()
        {
            _step = Step.Evacuate;
            _screen.ShowToast(_texts[TextId.ToastDocumentsSaved]);
            _setup.Grandma.SetCanBeCarried(true);
            _setup.Window.SetReady(true);
            _screen.SetObjective(_texts[TextId.ObjectiveEvacuateGrandma]);
            Talk(new[]
            {
                _dialogue.Line("G06", _setup.Grandma.Head, _playerHead),
                _dialogue.Line("M04", _playerHead, _setup.Grandma.Head),
            });
        }

        private void HandleResidentPickedUp(Rescuable resident) =>
            _nav.SetRoute(new[] { _setup.Window.transform }, null, _texts[TextId.NavWindow]);

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident != _setup.Grandma)
            {
                return;
            }

            _step = Step.Done;
            _screen.SetObjective(_texts[TextId.ObjectiveGoToFloor3]);
            if (_setup.RouteUp.Length > 0)
            {
                _nav.SetRoute(_setup.RouteUp, null, _texts[TextId.NavFloor3]);
            }
            _screen.Say(_dialogue.Library.Get("P08"));
            Finish();
        }


        private void Talk(params DialogueLine[] lines) => Talk(lines, null);

        private void Talk(DialogueLine[] lines, Action onFinished)
        {
            _dialogue.Play(lines, onFinished);
        }

        private void Bubble(string id) => _screen.Say(_dialogue.Library.Get(id), _setup.Grandma.Head);
    }
}
