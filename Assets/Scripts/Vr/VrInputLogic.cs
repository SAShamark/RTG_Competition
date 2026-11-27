using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;

namespace Vr
{
    [Serializable]
    public sealed class VrInputLogic
    {
        private const string LeftHand = "<XRController>{LeftHand}/";
        private const string RightHand = "<XRController>{RightHand}/";
        private const string StickDeadzone = "stickDeadzone";
        private const uint HapticChannel = 0;

        private readonly InputAction _move = Stick("Move", LeftHand);
        private readonly InputAction _turn = Stick("Turn", RightHand);
        private readonly InputAction _primary = Button("Primary", RightHand + "{TriggerButton}");
        private readonly InputAction _leftPrimary = Button("LeftPrimary", LeftHand + "{TriggerButton}");
        private readonly InputAction _confirm = Button("Confirm", RightHand + "{PrimaryButton}");
        private readonly InputAction _altInteract = Button("AltInteract", RightHand + "{SecondaryButton}");
        private readonly InputAction _radio = Button("Radio", LeftHand + "{SecondaryButton}");
        private readonly InputAction _run = Button("Run", LeftHand + "{Primary2DAxisClick}");
        private readonly InputAction _jump = Button("Jump", RightHand + "{PrimaryButton}", RightHand + "{Primary2DAxisClick}");
        private readonly InputAction _pause = Button("Pause", LeftHand + "{MenuButton}", LeftHand + "{PrimaryButton}");
        private readonly InputAction _leftGripButton = Button("LeftGripButton", LeftHand + "{GripButton}");
        private readonly InputAction _rightGripButton = Button("RightGripButton", RightHand + "{GripButton}");
        private readonly InputAction _leftGrip = Axis("LeftGrip", LeftHand + "{Grip}");
        private readonly InputAction _rightGrip = Axis("RightGrip", RightHand + "{Grip}");
        private readonly InputAction _leftTrigger = Axis("LeftTrigger", LeftHand + "{Trigger}");
        private readonly InputAction _rightTrigger = Axis("RightTrigger", RightHand + "{Trigger}");
        private readonly InputAction _leftTracked = Tracked("LeftTracked", LeftHand);
        private readonly InputAction _rightTracked = Tracked("RightTracked", RightHand);

        private InputAction[] _all;

        private InputAction[] All => _all ??= new[]
        {
            _move, _turn, _primary, _leftPrimary, _confirm, _altInteract, _radio, _run, _jump, _pause, _leftGripButton, _rightGripButton,
            _leftGrip, _rightGrip, _leftTrigger, _rightTrigger, _leftTracked, _rightTracked,
        };

        public Vector2 Move => _move.ReadValue<Vector2>();
        public float Turn => _turn.ReadValue<Vector2>().x;
        public bool PrimaryPressed => _primary.WasPressedThisFrame();
        public bool IsPrimaryHeld => _primary.IsPressed();
        public bool ConfirmPressed => _confirm.WasPressedThisFrame();
        public bool IsConfirmHeld => _confirm.IsPressed();
        public bool AltInteractPressed => _altInteract.WasPressedThisFrame();
        public bool RadioPressed => _radio.WasPressedThisFrame();
        public bool IsRunHeld => _run.IsPressed();
        public bool JumpPressed => _jump.WasPressedThisFrame();
        public bool IsJumpHeld => _jump.IsPressed();
        public bool PausePressed => _pause.WasPressedThisFrame();
        public float LeftGrip => _leftGrip.ReadValue<float>();
        public float RightGrip => _rightGrip.ReadValue<float>();
        public float LeftTrigger => _leftTrigger.ReadValue<float>();
        public float RightTrigger => _rightTrigger.ReadValue<float>();
        public bool IsLeftTracked => _leftTracked.IsPressed();
        public bool IsRightTracked => _rightTracked.IsPressed();

        public bool AnyPressed
        {
            get
            {
                foreach (InputAction action in All)
                {
                    if (action.type == InputActionType.Button && action.WasPressedThisFrame())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsTriggerHeld(bool isLeft) => (isLeft ? _leftPrimary : _primary).IsPressed();

        public bool GripPressed(bool isLeft) => (isLeft ? _leftGripButton : _rightGripButton).WasPressedThisFrame();

        public bool IsGripHeld(bool isLeft) => (isLeft ? _leftGripButton : _rightGripButton).IsPressed();

        public void Pulse(bool isLeft, float amplitude, float duration)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(isLeft ? XRNode.LeftHand : XRNode.RightHand);
            if (device.isValid)
            {
                device.SendHapticImpulse(HapticChannel, Mathf.Clamp01(amplitude), duration);
            }
        }

        public void Enable()
        {
            foreach (InputAction action in All)
            {
                action.Enable();
            }
        }

        public void Disable()
        {
            foreach (InputAction action in All)
            {
                action.Disable();
            }
        }

        private static InputAction Stick(string name, string hand) =>
            new(name, InputActionType.Value, hand + "{Primary2DAxis}", processors: StickDeadzone, expectedControlType: "Vector2");

        private static InputAction Axis(string name, string binding) =>
            new(name, InputActionType.Value, binding, expectedControlType: "Axis");

        private static InputAction Tracked(string name, string hand) =>
            new(name, InputActionType.PassThrough, hand + "isTracked", expectedControlType: "Button");

        private static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
            {
                action.AddBinding(binding);
            }

            return action;
        }
    }
}
