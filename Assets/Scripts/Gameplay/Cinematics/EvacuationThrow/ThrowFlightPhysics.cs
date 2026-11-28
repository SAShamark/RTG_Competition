using System;
using System.Collections.Generic;
using Gameplay.Entities.Characters;
using Gameplay.Entities.Environment;
using UnityEngine;

namespace Gameplay.Cinematics.EvacuationThrow
{
    public enum ThrowContact
    {
        Air = 0,
        Cloth = 1,
        Ground = 2,
    }

    public readonly struct ThrowFrame
    {
        public readonly Vector3 Center;
        public readonly Quaternion Rotation;
        public readonly ThrowContact Contact;
        public readonly float Dent;
        public readonly float Velocity;

        public ThrowFrame(Vector3 center, Quaternion rotation, ThrowContact contact, float dent = 0f, float velocity = 0f)
        {
            Center = center;
            Rotation = rotation;
            Contact = contact;
            Dent = dent;
            Velocity = velocity;
        }
    }

    [Serializable]
    public sealed class ThrowFlightPhysics
    {
        private const float StepTime = 1f / 240f;
        private const float MaxContactTime = 1f;
        private const float MinRestitution = 0.05f;
        private const float MaxKneeDamping = 0.95f;
        private const float MinFriction = 0.05f;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinContactDepth = 0.05f;
        private const float MinSpeed = 0.1f;

        [SerializeField, Min(0f)] private float _throwRise = 0.9f;

        [Header("Cloth")]
        [SerializeField, Range(MinRestitution, 1f)] private float _restitution = 0.45f;
        [SerializeField, Min(0)] private int _clothBounces = 1;
        [SerializeField, Min(0f)] private float _bounceDrift = 0.25f;
        [SerializeField] private float _bounceSpin = 25f;
        [SerializeField, Min(0f)] private float _hopClearance = 0.35f;

        [Header("Ground")]
        [SerializeField, Min(MinFriction)] private float _friction = 0.6f;
        [SerializeField, Min(0f)] private float _kneeStiffness = 45f;
        [SerializeField, Range(0f, MaxKneeDamping)] private float _kneeDamping = 0.55f;
        [SerializeField, Min(0f)] private float _settleTime = 0.6f;
        [SerializeField] private float _stumbleLean = 12f;

        private readonly List<ThrowFrame> _frames = new();
        private float _gravity;
        private float _lead;
        private bool _isPressingCloth;
        private float _clothVelocity;

        public ThrowFlightPhysics()
        {
        }

        public ThrowFlightPhysics(float throwRise)
        {
            _throwRise = throwRise;
        }

        public float FlightTime { get; private set; }
        public float Duration => Mathf.Max(0, _frames.Count - 1) * StepTime;

        public void Build(Vector3 launch, Vector3 landing, Vector3 safe, Quaternion launchRotation,
            Quaternion lyingRotation, Quaternion safeRotation, float contactDepth)
        {
            _frames.Clear();
            _lead = 0f;
            _isPressingCloth = false;
            _gravity = -Physics.gravity.y;

            Vector3 drift = Vector3.ProjectOnPlane(safe - landing, Vector3.up);
            drift = drift.sqrMagnitude > MinDirectionSqr ? drift.normalized : Vector3.ProjectOnPlane(landing - launch, Vector3.up).normalized;

            float rise = Mathf.Sqrt(2f * _gravity * _throwRise);
            FlightTime = FallTime(launch.y, rise, landing.y);
            AddAir(launch, landing, rise, FlightTime, launchRotation, lyingRotation);

            float impact = Mathf.Max(MinSpeed, _gravity * FlightTime - rise);
            // One spring for every hit: stiff enough that the first, hardest impact dips exactly to the cloth's limit.
            float omega = impact / Mathf.Max(MinContactDepth, contactDepth);
            // Damping ratio of a spring that gives back e of the speed it took: zeta = -ln e / sqrt(pi^2 + ln^2 e).
            float logE = Mathf.Log(_restitution);
            float zeta = -logE / Mathf.Sqrt(Mathf.PI * Mathf.PI + logE * logE);

            Vector3 point = landing;
            Quaternion rotation = lyingRotation;
            for (int i = 0; i < _clothBounces; i++)
            {
                float exit = AddContact(point, rotation, impact, omega, zeta);
                Vector3 next = point + drift * _bounceDrift;
                Quaternion nextRotation = Quaternion.AngleAxis(_bounceSpin, Vector3.up) * rotation;
                AddAir(point, next, exit, 2f * exit / _gravity, rotation, nextRotation);
                impact = exit;
                point = next;
                rotation = nextRotation;
            }

            float hop = AddContact(point, rotation, impact, omega, zeta);
            float needed = safe.y - point.y + _hopClearance;
            if (needed > 0f)
            {
                hop = Mathf.Max(hop, Mathf.Sqrt(2f * _gravity * needed));
            }

            float hopTime = FallTime(point.y, hop, safe.y);
            Vector3 toSafe = Vector3.ProjectOnPlane(safe - point, Vector3.up);
            // Touch down short of the safe point by the friction slide: v*t + v^2 / (2*mu*g) = d, solved for v.
            float slide = 1f / (2f * _friction * _gravity);
            float distance = toSafe.magnitude;
            float slideSpeed = (-hopTime + Mathf.Sqrt(hopTime * hopTime + 4f * slide * distance)) / (2f * slide);
            Vector3 direction = distance > 0f ? toSafe / distance : drift;
            Vector3 touchdown = point + direction * (slideSpeed * hopTime);
            touchdown.y = safe.y;

            AddAir(point, touchdown, hop, hopTime, rotation, safeRotation);
            AddSettle(touchdown, direction, slideSpeed, _gravity * hopTime - hop, safeRotation);
        }

        public void Pose(float time, CharacterControl body, Trampoline cloth)
        {
            ThrowFrame frame = Sample(time);
            body.SetPose(frame.Contact == ThrowContact.Ground ? CharacterPose.Animated : CharacterPose.Flailing);
            body.SetCenterPose(frame.Center, frame.Rotation);

            bool isOnCloth = frame.Contact == ThrowContact.Cloth;
            if (isOnCloth)
            {
                cloth.Press(frame.Center, frame.Dent);
                _clothVelocity = frame.Velocity;
            }
            else if (_isPressingCloth)
            {
                cloth.Release(_clothVelocity);
            }

            _isPressingCloth = isOnCloth;
        }

        public void LetGoOfCloth(Trampoline cloth)
        {
            if (_isPressingCloth)
            {
                _isPressingCloth = false;
                cloth.Release(0f);
            }
        }

        private ThrowFrame Sample(float time)
        {
            float position = Mathf.Max(0f, time) / StepTime;
            int index = Mathf.Min((int)position, _frames.Count - 1);
            int nextIndex = Mathf.Min(index + 1, _frames.Count - 1);
            float blend = position - index;
            ThrowFrame from = _frames[index];
            ThrowFrame to = _frames[nextIndex];
            return new ThrowFrame(Vector3.Lerp(from.Center, to.Center, blend),
                Quaternion.Slerp(from.Rotation, to.Rotation, blend), from.Contact, from.Dent, from.Velocity);
        }

        // y0 + v*t - g*t^2/2 = y1, the later root.
        private float FallTime(float fromY, float upSpeed, float toY) =>
            (upSpeed + Mathf.Sqrt(Mathf.Max(0f, upSpeed * upSpeed + 2f * _gravity * (fromY - toY)))) / _gravity;

        // Free flight: the centre of mass follows the ballistic curve and the body spins at a constant rate.
        private void AddAir(Vector3 from, Vector3 to, float upSpeed, float duration, Quaternion fromRotation, Quaternion toRotation)
        {
            Vector3 flat = Vector3.ProjectOnPlane(to - from, Vector3.up) / duration;
            float time = _lead;
            for (; time < duration; time += StepTime)
            {
                Vector3 center = from + flat * time + Vector3.up * (upSpeed * time - 0.5f * _gravity * time * time);
                _frames.Add(new ThrowFrame(center, Quaternion.SlerpUnclamped(fromRotation, toRotation, time / duration), ThrowContact.Air));
            }

            _lead = time - duration;
        }

        // The cloth is a damped spring under the body: x'' = -w^2 x - 2 zeta w x' - g, until it pushes the body back off.
        private float AddContact(Vector3 point, Quaternion rotation, float speed, float omega, float zeta)
        {
            float dent = 0f;
            float velocity = -speed;
            for (float time = 0f; time < MaxContactTime; time += StepTime)
            {
                velocity += (-omega * omega * dent - 2f * zeta * omega * velocity - _gravity) * StepTime;
                dent += velocity * StepTime;
                if (dent >= 0f && velocity > 0f)
                {
                    break;
                }

                _frames.Add(new ThrowFrame(point + Vector3.up * dent, rotation, ThrowContact.Cloth, dent, velocity));
            }

            _lead = 0f;
            return Mathf.Max(0f, velocity);
        }

        private void AddSettle(Vector3 touchdown, Vector3 direction, float slideSpeed, float fallSpeed, Quaternion rotation)
        {
            float braking = _friction * _gravity;
            float stopTime = slideSpeed / braking;
            float damped = _kneeStiffness * Mathf.Sqrt(1f - _kneeDamping * _kneeDamping);
            Vector3 leanAxis = Vector3.Cross(Vector3.up, direction);
            float duration = Mathf.Max(_settleTime, stopTime);

            for (float time = _lead; time < duration; time += StepTime)
            {
                float sliding = Mathf.Min(time, stopTime);
                float along = slideSpeed * sliding - 0.5f * braking * sliding * sliding;
                // Knees soak up the fall: a damped spring leaving rest at the touchdown speed, x = -v/wd * e^(-zeta w t) * sin(wd t).
                float dip = damped > 0f
                    ? -fallSpeed / damped * Mathf.Exp(-_kneeDamping * _kneeStiffness * time) * Mathf.Sin(damped * time)
                    : 0f;
                float lean = stopTime > 0f ? _stumbleLean * Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / stopTime)) : 0f;
                _frames.Add(new ThrowFrame(touchdown + direction * along + Vector3.up * dip,
                    Quaternion.AngleAxis(lean, leanAxis) * rotation, ThrowContact.Ground));
            }

            _lead = 0f;
        }
    }
}
