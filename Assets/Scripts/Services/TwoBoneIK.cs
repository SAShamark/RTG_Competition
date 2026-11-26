using UnityEngine;

namespace Services
{
    public static class TwoBoneIK
    {
        private const float MinReach = 0.001f;

        public static void Solve(Transform upper, Transform lower, Transform hand, Vector3 grip, Quaternion rotation, float weight)
        {
            Vector3 target = Vector3.Lerp(hand.position, grip, weight);
            Vector3 toUpper = upper.position - lower.position;
            Vector3 toHand = hand.position - lower.position;
            float upperLength = toUpper.magnitude;
            float foreLength = toHand.magnitude;
            float reach = Mathf.Clamp(Vector3.Distance(upper.position, target), Mathf.Abs(upperLength - foreLength) + MinReach, upperLength + foreLength - MinReach);
            // Law of cosines: cos(elbow) = (a² + b² − reach²) / 2ab puts the wrist exactly that far from the shoulder.
            float bend = Mathf.Acos(Mathf.Clamp((upperLength * upperLength + foreLength * foreLength - reach * reach) / (2f * upperLength * foreLength), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 hinge = Vector3.Cross(toUpper, toHand).normalized;
            lower.rotation = Quaternion.AngleAxis(bend - Vector3.Angle(toUpper, toHand), hinge) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(hand.position - upper.position, target - upper.position) * upper.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, rotation, weight);
        }
    }
}
