using System;

namespace Gameplay.Mission.Core
{
    public abstract class MissionStep : MissionPart, IDisposable
    {
        protected MissionStep(MissionContext context) : base(context)
        {
        }

        public event Action Completed;

        public abstract string RadioHint { get; }

        public abstract void Enter();

        public virtual void Dispose()
        {
        }

        public virtual void Tick(float deltaTime)
        {
        }

        protected void Complete() => Completed?.Invoke();
    }
}
