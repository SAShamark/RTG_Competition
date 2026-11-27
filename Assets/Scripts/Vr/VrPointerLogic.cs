using System;
using UnityEngine;

namespace Vr
{
    [Serializable]
    public sealed class VrPointerLogic
    {
        [SerializeField] private LineRenderer _laser;
        [SerializeField] private Transform _cursor;
        [SerializeField, Min(0.1f)] private float _maxDistance = 6f;
        [SerializeField, Min(0f)] private float _cursorLift = 0.005f;
        [SerializeField] private VrUiInputModule _uiModule;
        [SerializeField, Min(0.01f)] private float _touchDistance = 0.12f;

        private bool _isShown = true;

        public bool IsOverScreen { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Delta { get; private set; }

        public void Initialize() => SetShown(false);

        public void Tick(bool isActive, Transform aim, Transform screen, Vector2 resolution, bool isPressed, Transform touch, bool isTouchPressed)
        {
            bool wasOverScreen = IsOverScreen;
            IsOverScreen = false;
            if (!isActive)
            {
                SetShown(false);
                Release();
                return;
            }

            var plane = new Plane(-screen.forward, screen.position);
            if (TryTouch(touch, screen, plane, resolution, wasOverScreen, isTouchPressed))
            {
                SetShown(false);
                return;
            }

            SetShown(true);
            var ray = new Ray(aim.position, aim.forward);
            Vector3 end = ray.GetPoint(_maxDistance);
            if (plane.Raycast(ray, out float distance) && distance <= _maxDistance)
            {
                Vector3 hit = ray.GetPoint(distance);
                Vector3 local = screen.InverseTransformPoint(hit);
                var uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
                if (uv.x is >= 0f and <= 1f && uv.y is >= 0f and <= 1f)
                {
                    end = hit;
                    IsOverScreen = true;
                    Vector2 position = Vector2.Scale(uv, resolution);
                    Delta = wasOverScreen ? position - Position : Vector2.zero;
                    Position = position;
                    _uiModule.SetPointer(true, position, isPressed);
                    _cursor.SetPositionAndRotation(hit - screen.forward * _cursorLift, screen.rotation);
                }
            }

            if (!IsOverScreen)
            {
                Release();
            }

            _cursor.gameObject.SetActive(IsOverScreen);
            _laser.SetPosition(0, aim.position);
            _laser.SetPosition(1, end);
        }

        private bool TryTouch(Transform touch, Transform screen, Plane plane, Vector2 resolution, bool wasOverScreen, bool isPressed)
        {
            Vector3 point = touch.position;
            if (Mathf.Abs(plane.GetDistanceToPoint(point)) > _touchDistance)
            {
                return false;
            }

            Vector3 local = screen.InverseTransformPoint(plane.ClosestPointOnPlane(point));
            var uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
            if (uv.x is < 0f or > 1f || uv.y is < 0f or > 1f)
            {
                return false;
            }

            IsOverScreen = true;
            Vector2 position = Vector2.Scale(uv, resolution);
            Delta = wasOverScreen ? position - Position : Vector2.zero;
            Position = position;
            _uiModule.SetPointer(true, position, isPressed);
            _cursor.gameObject.SetActive(true);
            _cursor.SetPositionAndRotation(plane.ClosestPointOnPlane(point) - screen.forward * _cursorLift, screen.rotation);
            return true;
        }

        private void Release()
        {
            Delta = Vector2.zero;
            _uiModule.SetPointer(false, Position, false);
        }

        private void SetShown(bool isShown)
        {
            if (isShown == _isShown)
            {
                return;
            }

            _isShown = isShown;
            _laser.enabled = isShown;
            if (!isShown)
            {
                _cursor.gameObject.SetActive(false);
            }
        }
    }
}
