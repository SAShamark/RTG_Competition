using System;

namespace Gameplay.Mission.Core
{
    public abstract class MissionStage : MissionPart, IDisposable
    {
        private MissionStep[] _steps;
        private int _index = -1;
        private bool _isAdvancing;
        private bool _hasPendingStep;

        protected MissionStage(MissionContext context) : base(context)
        {
        }

        public event Action Finished;

        public bool IsStarted => _index >= 0;
        public bool IsActive => IsStarted && !IsFinished;
        public bool IsFinished => _index >= _steps.Length;
        public virtual string RadioHint => IsActive ? Current.RadioHint : "";

        private MissionStep Current => _steps[_index];

        public void Init()
        {
            OnInit();
            _steps = CreateSteps();
        }

        public void Dispose()
        {
            if (IsActive)
            {
                Current.Completed -= RequestNextStep;
                Current.Dispose();
            }

            OnDispose();
        }

        public void Begin()
        {
            if (IsStarted)
            {
                return;
            }

            OnBegin();
            RequestNextStep();
        }

        public void Tick(float deltaTime)
        {
            if (IsActive)
            {
                Current.Tick(deltaTime);
            }
        }

        protected abstract MissionStep[] CreateSteps();

        protected virtual void OnInit()
        {
        }

        protected virtual void OnDispose()
        {
        }

        protected virtual void OnBegin()
        {
        }

        protected virtual void OnFinished()
        {
        }

        // A step may complete inside its own Enter; the loop moves on only after Enter returns.
        private void RequestNextStep()
        {
            _hasPendingStep = true;
            if (_isAdvancing)
            {
                return;
            }

            _isAdvancing = true;
            while (_hasPendingStep)
            {
                _hasPendingStep = false;
                MoveNext();
            }

            _isAdvancing = false;
        }

        private void MoveNext()
        {
            if (IsActive)
            {
                Current.Completed -= RequestNextStep;
                Current.Dispose();
            }

            _index++;
            if (IsFinished)
            {
                OnFinished();
                Finished?.Invoke();
                return;
            }

            Current.Completed += RequestNextStep;
            Current.Enter();
        }
    }
}
