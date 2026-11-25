using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandbox.WaterStream
{
    public sealed class WaterStreamShowcase : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private WaterStreamTestRig[] _handheld;
        [SerializeField] private WaterStreamTestRig[] _stands;
        [SerializeField] private TextMesh _label;
        [SerializeField] private string[] _names;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _pitchLimit = 85f;

        private int _current;
        private bool _areStandsOn = true;
        private float _pitch;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Select(0);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            for (int i = 0; i < _handheld.Length; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Select(i);
                }
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                _areStandsOn = !_areStandsOn;
                foreach (WaterStreamTestRig stand in _stands)
                {
                    stand.SetAuto(_areStandsOn);
                }
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            _handheld[_current].SetSpraying(mouse.leftButton.isPressed);
            Look(mouse.delta.ReadValue());
            Move(keyboard, Time.deltaTime);
        }

        private void Select(int index)
        {
            _handheld[_current].SetSpraying(false);
            _current = index;
            _label.text = _names[index];
        }

        private void Look(Vector2 delta)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            _pitch = Mathf.Clamp(_pitch - delta.y * _lookSensitivity, -_pitchLimit, _pitchLimit);
            transform.Rotate(0f, delta.x * _lookSensitivity, 0f);
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move(Keyboard keyboard, float deltaTime)
        {
            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float right = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            Vector3 move = transform.forward * forward + transform.right * right;
            transform.position += Vector3.ClampMagnitude(move, 1f) * (_moveSpeed * deltaTime);
        }
    }
}
