using System;
using Game.Fire;
using Game.Items;
using Game.World;
using UI.Screens.Variants.Gameplay;

namespace Game.Scenes
{
    public class Floor1Controller
    {
        private enum Step { Waiting, OpenCabinet, PutOut, Done }

        private readonly GameplayScreen _screen;
        private readonly Hatch _cabinet;
        private readonly HeldItemLogic _items;
        private readonly FireSource[] _fires;

        private Step _step = Step.Waiting;

        public bool IsActive => _step != Step.Waiting && _step != Step.Done;

        public string RadioHint => _step switch
        {
            Step.OpenCabinet => "Вогнегасник у червоному щитку на стіні. Відчиняй і бери!",
            Step.PutOut => "Затисни ЛКМ і гаси вогонь біля сходів. Цілься в основу полум'я!",
            _ => "",
        };

        public event Action OnFinished;

        public Floor1Controller(GameplayScreen screen, Hatch cabinet, HeldItemLogic items, FireSource[] fires)
        {
            _screen = screen;
            _cabinet = cabinet;
            _items = items;
            _fires = fires;
        }

        public void Init()
        {
            _items.OnItemChanged += HandleItemChanged;
            _cabinet.OnOpened += HandleCabinetOpened;

            foreach (FireSource fire in _fires)
            {
                fire.OnExtinguished += HandleFireOut;
            }
        }

        public void Dispose()
        {
            _items.OnItemChanged -= HandleItemChanged;
            _cabinet.OnOpened -= HandleCabinetOpened;

            foreach (FireSource fire in _fires)
            {
                fire.OnExtinguished -= HandleFireOut;
            }
        }

        public void Begin()
        {
            if (_step != Step.Waiting)
            {
                return;
            }

            if (AllOut())
            {
                _step = Step.Done;
                return;
            }

            bool hasExtinguisher = _items.Owns(ItemType.Extinguisher);
            _step = hasExtinguisher ? Step.PutOut : Step.OpenCabinet;
            _screen.SetObjective(hasExtinguisher ? "Загасити вогонь біля сходів" : "Взяти вогнегасник зі щитка");
        }

        private void HandleCabinetOpened(Hatch cabinet)
        {
        }

        private void HandleItemChanged(ItemType item)
        {
            if (item != ItemType.Extinguisher || _step == Step.Done || AllOut())
            {
                return;
            }

            if (_step == Step.OpenCabinet)
            {
                _step = Step.PutOut;
                _screen.SetObjective("Загасити вогонь біля сходів");
                _screen.PartnerSay("Бери його — і до вогню!", 2.5f);
            }
        }

        private void HandleFireOut(FireSource fire)
        {
            if (_step == Step.Done || !AllOut())
            {
                return;
            }

            _step = Step.Done;
            _screen.SetObjective("Піднятися на другий поверх");
            _screen.PartnerSay("Прохід чистий! Підіймайся, там ще люди!", 3.5f);
            OnFinished?.Invoke();
        }

        private bool AllOut()
        {
            foreach (FireSource fire in _fires)
            {
                if (!fire.IsOut)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
