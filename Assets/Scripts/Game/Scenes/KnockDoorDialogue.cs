using System;
using System.Threading;
using Game.Items;
using Game.World;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Game.Scenes
{
    public class KnockDoorDialogue : IDisposable
    {
        private readonly struct Line
        {
            public readonly string Speaker;
            public readonly string Text;
            public readonly float Hold;

            public Line(string speaker, string text, float hold)
            {
                Speaker = speaker;
                Text = text;
                Hold = hold;
            }
        }

        private const string Mahmud = "Махмуд";
        private const string Resident = "Голос за дверима";

        private static readonly Line[] Opening =
        {
            new(Mahmud, "Відчиніть! Ми прийшли вас рятувати!", 2.6f),
            new(Resident, "У Бога не вірю! Проповідників не пускаю!", 2.8f),
        };

        private static readonly Line[] WithExtinguisher =
        {
            new(Mahmud, "Я теж на нього не розраховую. Тому взяв вогнегасник.", 3.2f),
            new(Resident, "А-а, то ви продаєте. Нічого не треба!", 2.8f),
        };

        private static readonly Line[] WithoutExtinguisher =
        {
            new(Mahmud, "...Добре. Повернуся з аргументом.", 2.6f),
        };

        private readonly Door _door;
        private readonly GameplayScreen _gameplayScreen;
        private readonly HeldItemLogic _items;
        private readonly AudioSource _latchAudio;
        private readonly CancellationTokenSource _cancellation = new();

        private bool _isPlaying;
        private bool _openingHeard;
        private bool _isFinished;

        public event Action OnFinished;

        public KnockDoorDialogue(Door door, GameplayScreen gameplayScreen, HeldItemLogic items, AudioSource latchAudio)
        {
            _door = door;
            _gameplayScreen = gameplayScreen;
            _items = items;
            _latchAudio = latchAudio;
        }

        public void Init()
        {
            _door.SetLocked(true);
            _door.SetLockedPrompt("Постукати", string.Empty);
            _door.OnLockedAttempt += HandleKnock;
        }

        public void Dispose()
        {
            _door.OnLockedAttempt -= HandleKnock;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private void HandleKnock(Door door)
        {
            if (_isPlaying)
            {
                return;
            }

            if (_isFinished)
            {
                _gameplayScreen.ShowToast("За дверима мовчать. Їм «нічого не треба».");
                return;
            }

            PlayAsync(_items.Owns(ItemType.Extinguisher));
        }

        private async void PlayAsync(bool hasExtinguisher)
        {
            _isPlaying = true;
            try
            {
                if (!_openingHeard || hasExtinguisher)
                {
                    await PlayLines(Opening);
                    _openingHeard = true;
                }

                if (!hasExtinguisher)
                {
                    await PlayLines(WithoutExtinguisher);
                    return;
                }

                await PlayLines(WithExtinguisher);
                if (_latchAudio != null)
                {
                    _latchAudio.Play();
                }

                _gameplayScreen.ShowToast("*клац* — засувка");
                _isFinished = true;
                _door.SetLockedPrompt("Постукати", "нічого не треба");
                OnFinished?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isPlaying = false;
            }
        }

        private async Awaitable PlayLines(Line[] lines)
        {
            foreach (Line line in lines)
            {
                _gameplayScreen.PartnerSay($"<b>{line.Speaker}:</b> {line.Text}", line.Hold);
                await Awaitable.WaitForSecondsAsync(line.Hold + 0.3f, _cancellation.Token);
            }
        }
    }
}
