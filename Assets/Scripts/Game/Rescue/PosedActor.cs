using UnityEngine;

namespace Game.Rescue
{
    public class PosedActor : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private ResidentPoseLogic _pose = new();

        public float BodyCenterHeight => 0.85f;

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
