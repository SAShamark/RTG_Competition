using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission
{
    [Serializable]
    public sealed class MissionStats
    {
        private const float FullPercent = 100f;

        [SerializeField] private MoneyPickup[] _money = Array.Empty<MoneyPickup>();
        [SerializeField] private FireSource[] _fires = Array.Empty<FireSource>();

        private GameplayScreen _screen;
        private TextLibrary _texts;

        public bool IsRunning { get; private set; }
        public float Time { get; private set; }
        public int MoneyFound { get; private set; }

        public float FirePercentOut
        {
            get
            {
                if (_fires.Length == 0)
                {
                    return FullPercent;
                }

                int outCount = 0;
                foreach (FireSource fire in _fires)
                {
                    outCount += fire.IsOut ? 1 : 0;
                }

                return FullPercent * outCount / _fires.Length;
            }
        }

        public void Init(GameplayScreen screen, TextLibrary texts)
        {
            _screen = screen;
            _texts = texts;
            foreach (MoneyPickup money in _money)
            {
                money.Taken += HandleMoneyTaken;
            }
        }

        public void Dispose()
        {
            foreach (MoneyPickup money in _money)
            {
                money.Taken -= HandleMoneyTaken;
            }
        }

        public void Begin() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Tick(float deltaTime)
        {
            if (IsRunning)
            {
                Time += deltaTime;
            }
        }

        private void HandleMoneyTaken(ItemPickup item)
        {
            var money = (MoneyPickup)item;
            MoneyFound += money.Amount;
            _screen.ShowToast(_texts.Format(TextId.ToastMoney, money.Amount));
        }
    }
}
