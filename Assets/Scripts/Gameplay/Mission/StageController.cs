using System;

namespace Gameplay.Mission
{
    public abstract class StageController : IDisposable
    {
        public abstract bool IsActive { get; }
        public abstract string RadioHint { get; }

        public event Action Finished;

        public abstract void Init();

        public abstract void Dispose();

        public virtual void Tick(float deltaTime)
        {
        }

        protected void Finish() => Finished?.Invoke();
    }
}
