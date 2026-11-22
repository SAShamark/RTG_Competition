using UnityEngine;

namespace Services
{
    public static class EffectPlayback
    {
        // Effect scripts time their lights and shakes from OnEnable, so the object is re-enabled on every play.
        public static void Restart(ParticleSystem effect)
        {
            GameObject effectObject = effect.gameObject;
            effectObject.SetActive(false);
            effectObject.SetActive(true);
            effect.Play(true);
        }
    }
}
