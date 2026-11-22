using UnityEngine;

namespace Gameplay.Entities.Residents
{
    public sealed class PosedActor : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private ResidentPoseLogic _pose = new();
        [SerializeField] private float _bodyCenterHeight = 0.85f;

        public float BodyCenterHeight => _bodyCenterHeight;

        private void Awake()
        {
            _pose.Init(_animator);
        }

        public void SetPose(ResidentPose pose) => _pose.SetMode(pose);

        public void SetCenterPose(Vector3 center, Quaternion rotation)
        {
            transform.SetPositionAndRotation(center - rotation * Vector3.up * BodyCenterHeight, rotation);
        }

        private void LateUpdate() => _pose.Tick(Time.deltaTime);
    }
}
