using System;
using Game.Fire;
using Game.Items.Extinguisher;
using Game.Rescue;
using Game.World;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Game.Scenes
{
    public class Floor3Controller
    {
        private enum Step { Waiting, FindNeighbor, Kitchen, Evacuate, Done }

        private const string NeighborName = "Сусід";
        private const string PlayerName = "Махмуд";

        private static readonly string[] CarryLines =
        {
            "Обережно, в мене спина!",
            "А маховики ти назад поставиш?",
            "Я взагалі-то сам можу... Ні, не можу.",
        };

        private readonly GameplayScreen _screen;
        private readonly DialogueDirector _dialogue;
        private readonly Transform _playerHead;
        private readonly ExtinguisherLogic _extinguisher;
        private readonly NavigationGuide _nav;
        private readonly Transform[] _routeToFlat;
        private readonly Collider _flatZone;
        private readonly Transform[] _routeToPantry;
        private readonly Collider _pantryZone;
        private readonly Transform[] _routeToKitchen;
        private readonly Collider _kitchenZone;
        private readonly Transform[] _routeUp;
        private readonly FireSource _stoveFire;
        private readonly GasValve _valve;
        private readonly QuestItemPickup _handle;
        private readonly Rescuable _neighbor;
        private readonly EvacuationWindow _window;
        private readonly EvacuationController _evacuation;
        private readonly Transform _partner;
        private readonly Transform _partnerSpot;

        private Step _step = Step.Waiting;
        private bool _seenNeighbor;
        private bool _gasHinted;
        private bool _hasHandle;
        private bool _gasClosed;
        private int _carryLine;
        private float _carryTimer = -1f;
        private float _burstTimer = -1f;
        private float _blastTimer = -1f;

        public bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public string RadioHint
        {
            get
            {
                if (_step == Step.FindNeighbor)
                {
                    return "Кричать з третього поверху! Прочинені двері праворуч від сходів.";
                }

                if (_step == Step.Kitchen)
                {
                    if (!_gasClosed && !_hasHandle)
                    {
                        return "Вентилі в коморі. Замок — відмичкою, а маховики пошукай там же — у ящику й під мотлохом!";
                    }

                    if (!_gasClosed)
                    {
                        return "Маховики є — став на вентилі. І дивись на схему: спершу витяжка, потім газ!";
                    }

                    return "Газ перекрито — догашуй кухню!";
                }

                if (_step == Step.Evacuate)
                {
                    return "Батут під вікном кухні! Неси його до вікна й кидай!";
                }

                return "Четвертий поверх, Махмуде!";
            }
        }

        public event Action OnFinished;
        public event Action OnWheelsNeeded;

        public Floor3Controller(GameplayScreen screen, DialogueDirector dialogue, Transform playerHead, ExtinguisherLogic extinguisher,
            NavigationGuide nav, Transform[] routeToFlat, Collider flatZone, Transform[] routeToPantry, Collider pantryZone,
            Transform[] routeToKitchen, Collider kitchenZone, Transform[] routeUp, FireSource stoveFire, GasValve valve,
            QuestItemPickup handle, Rescuable neighbor, EvacuationWindow window,
            EvacuationController evacuation, Transform partner, Transform partnerSpot)
        {
            _screen = screen;
            _dialogue = dialogue;
            _playerHead = playerHead;
            _extinguisher = extinguisher;
            _nav = nav;
            _routeToFlat = routeToFlat;
            _flatZone = flatZone;
            _routeToPantry = routeToPantry;
            _pantryZone = pantryZone;
            _routeToKitchen = routeToKitchen;
            _kitchenZone = kitchenZone;
            _routeUp = routeUp;
            _stoveFire = stoveFire;
            _valve = valve;
            _handle = handle;
            _neighbor = neighbor;
            _window = window;
            _evacuation = evacuation;
            _partner = partner;
            _partnerSpot = partnerSpot;
        }

        public void Init()
        {
            _neighbor.SetCanBeCarried(false);
            _window.SetReady(false);
            _stoveFire.SetReignite(true);

            _extinguisher.OnFireHit += HandleFireHit;
            _stoveFire.OnExtinguished += HandleFireOut;
            _valve.SetRemoteOnly(true);
            _valve.OnClosed += HandleGasClosed;
            _handle.OnTaken += HandleHandleTaken;
            _neighbor.OnRefused += HandleNeighborRefused;
            _neighbor.OnPickedUp += HandleNeighborPickedUp;
            _evacuation.OnRescued += HandleRescued;
        }

        public void Dispose()
        {
            _extinguisher.OnFireHit -= HandleFireHit;
            _stoveFire.OnExtinguished -= HandleFireOut;
            _valve.OnClosed -= HandleGasClosed;
            _handle.OnTaken -= HandleHandleTaken;
            _neighbor.OnRefused -= HandleNeighborRefused;
            _neighbor.OnPickedUp -= HandleNeighborPickedUp;
            _evacuation.OnRescued -= HandleRescued;
        }

        public void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            _step = Step.FindNeighbor;
            _valve.StartHiss();
            _screen.SetObjective("Знайти мешканця");
            _screen.CharacterSay(NeighborName, "Рятуйте! Допоможіть, хто-небудь!", 3.5f);
            _nav.SetRoute(_routeToFlat, _flatZone, "Крик про допомогу");
        }

        public void Tick(float deltaTime)
        {
            if (_step == Step.FindNeighbor && _flatZone.bounds.Contains(_playerHead.position))
            {
                MeetNeighbor();
            }

            TickCarryLines(deltaTime);
            TickBlast(deltaTime);
        }

        private void TickBlast(float deltaTime)
        {
            if (_burstTimer > 0f)
            {
                _burstTimer -= deltaTime;
                if (_burstTimer <= 0f)
                {
                    _valve.Burst();
                    _screen.ShowToast("ПШШШШШ!");
                    _screen.CharacterSay(PlayerName, "Зараз рвоне! Я у вікно!", 2.5f);
                    _screen.SetObjective("Тікати!");
                    _blastTimer = 2.6f;
                }
            }
            else if (_blastTimer > 0f)
            {
                _blastTimer -= deltaTime;
                if (_blastTimer <= 0f)
                {
                    OnFinished?.Invoke();
                }
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
            _screen.SetObjective("Дістатися до мешканця");
            Talk(new[]
            {
                Neighbor("Вихід перекрито, допоможіть!"),
                Player("Це газ горить! Де його перекрити?"),
                Neighbor("Вентилі в коморі."),
            }, ShowPantryRoute);
        }

        private void ShowPantryRoute()
        {
            if (_gasClosed)
            {
                return;
            }

            _screen.SetObjective("Перекрити газ у коморі");
            OnWheelsNeeded?.Invoke();
            _nav.SetRoute(_routeToPantry, _pantryZone, "Комора");
        }

        private void HandleFireHit(FireSource fire)
        {
            if (fire != _stoveFire || _gasClosed || _gasHinted)
            {
                return;
            }

            _gasHinted = true;
            _screen.PartnerSay("Махмуде, стій! Це газ — поки не перекриєш, розгорятиметься знову. Вентилі — в коморі!", 5f);
        }

        private void HandleHandleTaken(QuestItemPickup handle)
        {
            _hasHandle = true;
            _screen.ShowToast("Маховик знайдено");
            if (!_gasClosed)
            {
                _screen.SetObjective("Поставити маховики на вентилі");
            }
        }

        private void HandleGasClosed()
        {
            _gasClosed = true;
            _screen.CharacterSay(NeighborName, "Газ перекрито! Гаси вогонь!", 3f);
            _stoveFire.SetReignite(false);
            _screen.SetObjective(_stoveFire.IsOut ? "Евакуювати мешканця" : "Загасити вогонь на кухні");
            if (!_stoveFire.IsOut)
            {
                _nav.SetRoute(_routeToKitchen, _kitchenZone, "Кухня");
            }
            else
            {
                _nav.Clear();
            }
            if (_stoveFire.IsOut)
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
            _neighbor.SetCanBeCarried(true);
            _window.SetReady(true);
            _partner.SetPositionAndRotation(_partnerSpot.position, _partnerSpot.rotation);

            _screen.SetObjective("Евакуювати мешканця");
            Talk(new[]
            {
                Player("Зараз я тебе врятую!"),
                Neighbor("А це безкоштовно?"),
            }, () => _screen.PartnerSay("Батут під вікном кухні! Кидай його мені, Махмуде!", 4f));
        }

        private void HandleNeighborRefused(Rescuable neighbor)
        {
        }

        private void HandleNeighborPickedUp(Rescuable neighbor)
        {
        }

        private void TickCarryLines(float deltaTime)
        {
            if (_carryTimer < 0f)
            {
                return;
            }

            if (_neighbor.State != RescueState.Carried)
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

        private void HandleRescued(Rescuable resident, int rescued)
        {
            if (resident != _neighbor)
            {
                return;
            }

            _step = Step.Done;
            _carryTimer = -1f;
            _nav.Clear();
            _screen.SetObjective("Вийти з будинку");
            _screen.PartnerSay("Є другий! Усі на вулиці — Махмуде, виходь звідти!", 3f);
            _burstTimer = 2.6f;
        }

        private DialogueLine Neighbor(string text) => new(NeighborName, _neighbor.Head, _playerHead, text);

        private DialogueLine Player(string text) => new(PlayerName, _playerHead, _neighbor.Head, text);

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

        private void Bubble(string text) => _screen.CharacterSay(NeighborName, text, 3f, _neighbor.Head);
    }
}
