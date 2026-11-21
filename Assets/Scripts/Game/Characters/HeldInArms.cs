using UnityEngine;

namespace Game.Characters
{
    public class HeldInArms : MonoBehaviour
    {
        private enum Hand { Left, Right }

        [SerializeField] private Animator _carrier;
        [SerializeField] private Hand _hand = Hand.Left;
        [SerializeField] private Vector3 _offset = new(0f, 0.05f, 0.08f);
        [SerializeField] private Vector3 _euler;
        [SerializeField] private float _wobble = 4f;

        private Transform _bone;
        private Vector3 _centerLocal;
        private float _phase;

        private void Awake()
        {
            _phase = Random.value * 10f;
            if (_carrier.isHuman)
            {
                _bone = _carrier.GetBoneTransform(_hand == Hand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                transform.SetParent(_bone, true);
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }

                _centerLocal = transform.InverseTransformPoint(bounds.center);
            }
        }

        private void LateUpdate()
        {
            if (_bone == null)
            {
                return;
            }

            Quaternion body = _carrier.transform.rotation;
            float jiggle = Mathf.Sin(Time.time * 14f + _phase) * _wobble;
            Quaternion rotation = body * Quaternion.Euler(_euler + new Vector3(jiggle, 0f, jiggle * 0.5f));
            Vector3 palm = _bone.position + body * _offset;
            Vector3 centerOffset = rotation * Vector3.Scale(_centerLocal, transform.lossyScale);
            transform.SetPositionAndRotation(palm - centerOffset, rotation);
        }
    }
}
