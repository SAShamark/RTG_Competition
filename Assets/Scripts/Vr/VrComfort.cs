using System;
using UnityEngine;

namespace Vr
{
    public enum VrTurnMode
    {
        Snap30,
        Snap45,
        Snap90,
        Smooth,
    }

    public enum VrVignetteLevel
    {
        Off,
        Low,
        High,
    }

    public enum VrMoveDirection
    {
        Head,
        Hand,
    }

    public static class VrComfort
    {
        private const string TurnModeKey = "VrTurnMode";
        private const string VignetteKey = "VrVignette";
        private const string MoveDirectionKey = "VrMoveDirection";
        private const float SmallSnap = 30f;
        private const float MediumSnap = 45f;
        private const float LargeSnap = 90f;

        public static VrTurnMode TurnMode { get; private set; } = VrTurnMode.Snap45;
        public static VrVignetteLevel Vignette { get; private set; } = VrVignetteLevel.Low;
        public static VrMoveDirection MoveDirection { get; private set; } = VrMoveDirection.Head;

        public static float SnapAngle => TurnMode switch
        {
            VrTurnMode.Snap30 => SmallSnap,
            VrTurnMode.Snap90 => LargeSnap,
            _ => MediumSnap,
        };

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            TurnMode = (VrTurnMode)PlayerPrefs.GetInt(TurnModeKey, (int)VrTurnMode.Snap45);
            Vignette = (VrVignetteLevel)PlayerPrefs.GetInt(VignetteKey, (int)VrVignetteLevel.Low);
            MoveDirection = (VrMoveDirection)PlayerPrefs.GetInt(MoveDirectionKey, (int)VrMoveDirection.Head);
        }

        public static void SetTurnMode(VrTurnMode mode)
        {
            TurnMode = mode;
            Save(TurnModeKey, (int)mode);
        }

        public static void SetVignette(VrVignetteLevel level)
        {
            Vignette = level;
            Save(VignetteKey, (int)level);
        }

        public static void SetMoveDirection(VrMoveDirection direction)
        {
            MoveDirection = direction;
            Save(MoveDirectionKey, (int)direction);
        }

        private static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Changed?.Invoke();
        }
    }
}
