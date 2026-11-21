using System;
using Game.Fire;
using Game.Rescue;
using Game.World;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Game.Scenes
{
    public class Floor2Controller
    {
        private enum Step { Waiting, FindGrandma, TalkToGrandma, GetDocuments, Evacuate, Done }

        private const string GrandmaName = "Бабуся";
        private const string PlayerName = "Махмуд";

        private static readonly string[] WrongPaintingLines =
        {
            "Це мій покійний Петро! Повісь, як було... Та ні, вже хай так.",
            "Там стіна, синку. Звичайна стіна.",
            "Обережно, то подарунок від сестри! Жахливий, але подарунок.",
            "Ні, не та! Ох, я й сама вже не пам'ятаю, за якою.",
        };

        private static readonly string[] CarryLines =
        {
            "Головою в двері не вдар!",
            "Документи сховав? Тоді кидай!",
            "Обережно, в мене тиск!",
            "А ти точно пожежник? Щось ти молодий.",
        };

        private readonly GameplayScreen _screen;
        private readonly DialogueDirector _dialogue;
        private readonly Transform _playerHead;
        private readonly TriggerZone _arrivalZone;
        private readonly Door _grandmaDoor;
        private readonly FireSource _doorFire;
        private readonly Rescuable _grandma;
        private readonly WallSafe _safe;
        private readonly WallPainting[] _paintings;
        private int _wrongPainting;
        private bool _safeFound;
        private readonly DocumentsStash _documents;
        private readonly EvacuationWindow _window;
        private readonly EvacuationController _evacuation;
        private readonly AudioSource _cough;
        private readonly NavigationGuide _nav;
        private readonly Transform[] _routeToFlat;
        private readonly Collider _flatZone;
        private readonly Transform[] _routeUp;
        private bool _fireOut;

        private Step _step = Step.Waiting;
        private bool _sawGrandma;
        private bool _explainedDocuments;
        private int _refusals;
        private int _carryLine;
        private float _carryTimer = -1f;
        private float _coughTimer = 2f;

        public bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public string RadioHint => _step switch
        {
            Step.FindGrandma => "Махмуде, кашель чути з лівої квартири! Двері ліворуч від сходів.",
            Step.TalkToGrandma => "Бачиш її? Гаси вогонь і виводь!",
            Step.GetDocuments => _safeFound
                ? "Сейф знайшов? Відкривай, бабуся без документів не піде!"
                : "Бабуся каже — документи за картиною. Пошукай сейф!",
            Step.Evacuate => "Батут під лівим вікном! Неси бабусю до вікна й кидай мені!",
            _ => "Третій поверх, Махмуде! Там хтось кричить!",
        };

        public event Action OnFinished;
        public event Action<WallSafe> OnSafePuzzleRequested;

        public Floor2Controller(GameplayScreen screen, DialogueDirector dialogue, Transform playerHead, TriggerZone arrivalZone,
            Door grandmaDoor, FireSource doorFire, Rescuable grandma, WallSafe safe, WallPainting[] paintings, DocumentsStash documents,
            EvacuationWindow window, EvacuationController evacuation, NavigationGuide nav, Transform[] routeToFlat, Collider flatZone,
            Transform[] routeUp)
        {
            _nav = nav;
            _routeToFlat = routeToFlat;
            _flatZone = flatZone;
            _routeUp = routeUp;
            _screen = screen;
            _dialogue = dialogue;
            _playerHead = playerHead;
            _arrivalZone = arrivalZone;
            _grandmaDoor = grandmaDoor;
            _doorFire = doorFire;
            _grandma = grandma;
            _safe = safe;
            _paintings = paintings;
            _documents = documents;
            _window = window;
            _evacuation = evacuation;
            _cough = grandma.GetComponent<AudioSource>();
        }

        public void Init()
        {
            _grandma.SetCanBeCarried(false);
            _window.SetReady(false);
            _cough.loop = false;
            _cough.Stop();

            _arrivalZone.OnEntered += HandleArrived;

            _grandmaDoor.OnOpened += HandleDoorOpened;

            _doorFire.OnExtinguished += HandleDoorFireOut;

            _grandma.OnRefused += HandleGrandmaRefused;
            _grandma.OnPickedUp += HandleGrandmaPickedUp;
            _safe.OnOpenRequested += HandleSafeOpenRequested;
            _safe.OnOpened += HandleSafeOpened;
            foreach (WallPainting painting in _paintings)
            {
                painting.OnMoved += HandlePaintingMoved;
            }

            _documents.OnTaken += HandleDocumentsTaken;
            _evacuation.OnRescued += HandleRescued;
        }

        public void Dispose()
        {
            _arrivalZone.OnEntered -= HandleArrived;

            _grandmaDoor.OnOpened -= HandleDoorOpened;

            _doorFire.OnExtinguished -= HandleDoorFireOut;

            _grandma.OnRefused -= HandleGrandmaRefused;
            _grandma.OnPickedUp -= HandleGrandmaPickedUp;
            _safe.OnOpenRequested -= HandleSafeOpenRequested;
            _safe.OnOpened -= HandleSafeOpened;
            foreach (WallPainting painting in _paintings)
            {
                painting.OnMoved -= HandlePaintingMoved;
            }

            _documents.OnTaken -= HandleDocumentsTaken;
            _evacuation.OnRescued -= HandleRescued;
        }

        public void Tick(float deltaTime)
        {
            TickCough(deltaTime);
            TickCarryLines(deltaTime);
        }

        public void ShowRouteToFlat()
        {
            if (_step != Step.Done && _routeToFlat.Length > 0)
            {
                _nav.SetRoute(_routeToFlat, _flatZone, "Бабуся");
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
                _cough.Play();
                _coughTimer = UnityEngine.Random.Range(6f, 9f);
            }
        }

        private void TickCarryLines(float deltaTime)
        {
            if (_carryTimer < 0f)
            {
                return;
            }

            if (_grandma.State != RescueState.Carried)
            {
                _carryTimer = -1f;
                return;
            }

            _carryTimer -= deltaTime;
            if (_carryTimer <= 0f)
            {
                Bubble(CarryLines[_carryLine++ % CarryLines.Length]);
                _carryTimer = 6f;
            }
        }

        private void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            _step = Step.FindGrandma;
            _coughTimer = 1.5f;
            _screen.SetObjective("Знайти мешканців другого поверху");
        }

        private void HandleArrived(TriggerZone zone) => Begin();

        private void HandleDoorOpened(Door door)
        {
            Begin();
            if (_sawGrandma)
            {
                return;
            }

            _sawGrandma = true;
            _step = Step.TalkToGrandma;
            _screen.SetObjective("Загасити вогонь і дістатися до бабусі");
            Talk(
                Grandma("Синку! Я тут, у спальні! Вогонь не пускає!"),
                Player("Не підходьте до вогню, бабусю! Зараз загашу!"));
        }

        private void HandleDoorFireOut(FireSource fire)
        {
            Begin();
            _fireOut = true;
            if (_step == Step.FindGrandma || _step == Step.TalkToGrandma)
            {
                _step = Step.TalkToGrandma;
                _screen.SetObjective("Вивести бабусю");
            }

            Bubble("Ох, дякую, синку! Іди-но сюди!");
        }

        private void HandleGrandmaRefused(Rescuable grandma)
        {
            Begin();
            if (!_explainedDocuments)
            {
                _explainedDocuments = true;
                _step = Step.GetDocuments;
                _screen.SetObjective(_safeFound ? "Відкрити сейф" : "Знайти документи за картиною");
                Talk(
                    Player("Бабусю, пожежна! Виходимо, швидко!"),
                    Grandma("Без документів не піду!"),
                    Player("Які документи, бабусю? Будинок горить!"),
                    Grandma("А я без них нікуди!"),
                    Player("Добре, де вони?"),
                    Grandma("У сейфі, за картиною."));
                return;
            }

            if (_step == Step.GetDocuments)
            {
                Bubble(++_refusals % 2 == 1
                    ? "Я сказала — без документів нікуди!"
                    : _safeFound ? "Сейф бачиш? Ну то відкривай!" : "Що ти на мене дивишся? Картини — он!");
            }
        }

        private void HandlePaintingMoved(WallPainting painting)
        {
            Begin();
            if (painting.HidesSafe)
            {
                _safeFound = true;
                if (_step == Step.GetDocuments)
                {
                    _screen.SetObjective("Відкрити сейф");
                }

                Bubble("Ото він! Код я не пам'ятаю... Дід казав: малюнок має зійтися.");
                return;
            }

            Bubble(WrongPaintingLines[_wrongPainting++ % WrongPaintingLines.Length]);
        }

        private void HandleSafeOpenRequested(WallSafe safe)
        {
            OnSafePuzzleRequested?.Invoke(safe);
        }

        public void SafePuzzleCancelled()
        {
            Bubble("Що, не сходиться? Дід казав — малюнок має бути цілий!");
        }

        private void HandleSafeOpened(WallSafe safe)
        {
            _screen.ShowToast("Сейф відкрито");
            if (_step == Step.GetDocuments)
            {
                _screen.SetObjective("Забрати документи");
            }

            Bubble("Ой, дякую, синку!");
        }

        private void HandleDocumentsTaken()
        {
            _step = Step.Evacuate;
            _screen.ShowToast("Документи збережено");
            _grandma.SetCanBeCarried(true);
            _window.SetReady(true);
            _screen.SetObjective("Евакуювати бабусю через вікно");
            Talk(new[]
            {
                Grandma("Ну от. Тепер можна й рятуватися."),
                Player("Тримайтеся міцніше. Підемо... коротким шляхом."),
            }, () => _screen.PartnerSay("Махмуде, сходами з нею не пройдеш! Я розклав батут під лівим вікном — кидай її мені!", 5f));
        }

        private void HandleGrandmaPickedUp(Rescuable grandma)
        {
            _carryTimer = 1.5f;
        }

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident != _grandma)
            {
                return;
            }

            _step = Step.Done;
            _carryTimer = -1f;
            _screen.SetObjective("Піднятися на третій поверх");
            if (_routeUp.Length > 0)
            {
                _nav.SetRoute(_routeUp, null, "3-й поверх");
            }
            _screen.CharacterSay(GrandmaName, "Я на таке не підписувалась!", 2.5f);
            _screen.PartnerSay("Бабусю прийняв, на третьому поверсі хтось лається, бігом туди!", 4f);
            OnFinished?.Invoke();
        }

        private DialogueLine Grandma(string text) => new(GrandmaName, _grandma.Head, _playerHead, text);

        private DialogueLine Player(string text) => new(PlayerName, _playerHead, _grandma.Head, text);

        private void Talk(params DialogueLine[] lines) => Talk(lines, null);

        private void Talk(DialogueLine[] lines, Action onFinished)
        {
            _dialogue.Play(lines, onFinished);
            return;

            foreach (DialogueLine line in lines)
            {
                _screen.CharacterSay(line.Speaker, line.Text, 3f);
            }

            onFinished?.Invoke();
        }

        private void Bubble(string text)
        {
            _screen.CharacterSay(GrandmaName, text, 3f, _grandma.Head);
        }
    }
}
