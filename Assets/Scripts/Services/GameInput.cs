using UnityEngine;
using UnityEngine.InputSystem;

namespace Services
{
    public static class GameInput
    {
        public const int NoSlot = -1;

        private static Keyboard Keys => Keyboard.current;
        private static Mouse Pointer => Mouse.current;

        public static Vector2 Look => Pointer.delta.ReadValue();
        public static Vector2 PointerPosition => Pointer.position.ReadValue();

        public static Vector2 Move
        {
            get
            {
                Vector2 input = Vector2.zero;
                input.y += Keys.wKey.isPressed ? 1f : 0f;
                input.y -= Keys.sKey.isPressed ? 1f : 0f;
                input.x += Keys.dKey.isPressed ? 1f : 0f;
                input.x -= Keys.aKey.isPressed ? 1f : 0f;
                return input;
            }
        }

        public static bool IsRunHeld => Keys.leftShiftKey.isPressed;
        public static bool JumpPressed => Keys.spaceKey.wasPressedThisFrame;
        public static bool IsJumpHeld => Keys.spaceKey.isPressed;
        public static bool PrimaryPressed => Pointer.leftButton.wasPressedThisFrame;
        public static bool IsPrimaryHeld => Pointer.leftButton.isPressed;
        public static bool InteractPressed => Keys.eKey.wasPressedThisFrame;
        public static bool AltInteractPressed => Keys.qKey.wasPressedThisFrame;
        public static bool DropPressed => Keys.gKey.wasPressedThisFrame;
        public static bool ViewTogglePressed => Keys.vKey.wasPressedThisFrame;
        public static bool RadioPressed => Keys.digit4Key.wasPressedThisFrame;
        public static bool CancelPressed => Keys.escapeKey.wasPressedThisFrame;
        public static bool AdvancePressed => Keys.spaceKey.wasPressedThisFrame || Pointer.leftButton.wasPressedThisFrame;
        public static bool AnyPressed => Keys.anyKey.wasPressedThisFrame || Pointer.leftButton.wasPressedThisFrame;

        public static bool IsSkipHeld =>
            Keys.spaceKey.isPressed || Keys.enterKey.isPressed || Keys.escapeKey.isPressed || Pointer.leftButton.isPressed;

        public static int HotbarSlot
        {
            get
            {
                if (Keys.digit1Key.wasPressedThisFrame)
                {
                    return 0;
                }

                if (Keys.digit2Key.wasPressedThisFrame)
                {
                    return 1;
                }

                return Keys.digit3Key.wasPressedThisFrame ? 2 : NoSlot;
            }
        }
    }
}
