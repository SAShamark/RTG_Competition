using UnityEngine;
using Vr;

namespace Services
{
    public static class GameInput
    {
        public static VrInputLogic Vr { get; set; }
        public static VrPointerLogic Pointer { get; set; }

        public static Vector2 Look => Pointer.Delta;
        public static Vector2 PointerPosition => Pointer.Position;
        public static Vector2 Move => Vr.Move;
        public static bool IsRunHeld => Vr.IsRunHeld;
        public static bool JumpPressed => Vr.JumpPressed;
        public static bool IsJumpHeld => Vr.IsJumpHeld;
        public static bool PrimaryPressed => Vr.PrimaryPressed;
        public static bool IsPrimaryHeld => Vr.IsPrimaryHeld;
        public static bool AltInteractPressed => Vr.AltInteractPressed;
        public static bool RadioPressed => Vr.RadioPressed;
        public static bool CancelPressed => Vr.PausePressed;
        public static bool AdvancePressed => Vr.ConfirmPressed || Vr.PrimaryPressed;
        public static bool AnyPressed => Vr.AnyPressed;
        public static bool IsSkipHeld => Vr.IsConfirmHeld || Vr.IsPrimaryHeld;
    }
}
