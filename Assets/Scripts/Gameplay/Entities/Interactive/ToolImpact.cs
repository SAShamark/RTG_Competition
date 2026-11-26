using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public static class ToolImpact
    {
        private const float MinThrownSpeed = 2f;

        public static void Apply(Collider collider, in ToolHit hit)
        {
            IToolReactive target = collider.GetComponentInParent<IToolReactive>();
            if (target != null)
            {
                target.React(hit);
                return;
            }

            Rigidbody body = collider.attachedRigidbody;
            if (body != null && !body.isKinematic && hit.Kind != ToolHitKind.Thrown)
            {
                body.AddForceAtPosition(hit.Direction * hit.Impulse, hit.Point, ForceMode.Impulse);
            }
        }

        public static void Collide(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < MinThrownSpeed)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            Apply(collision.collider, new ToolHit(ToolHitKind.Thrown, contact.point, -contact.normal, collision.impulse.magnitude));
        }
    }
}
