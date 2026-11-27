using UnityEngine;
using UnityEngine.XR.Management;

namespace Vr
{
    public static class VrBootstrap
    {
        private static XRManagerSettings Manager => XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;

        public static bool IsXrLoaded => Manager != null && Manager.activeLoader != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartXr()
        {
            XRManagerSettings manager = Manager;
            if (manager == null)
            {
                return;
            }

            if (manager.activeLoader == null)
            {
                manager.InitializeLoaderSync();
            }

            if (manager.activeLoader == null)
            {
                return;
            }

            manager.StartSubsystems();
            Application.quitting += StopXr;
        }

        private static void StopXr()
        {
            Application.quitting -= StopXr;
            if (IsXrLoaded)
            {
                Manager.StopSubsystems();
                Manager.DeinitializeLoader();
            }
        }
    }
}
