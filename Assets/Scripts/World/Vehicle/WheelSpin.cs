using UnityEngine;

public class WheelSpin : MonoBehaviour
{
    public Transform[] Wheels;
    public float Radius = 0.55f;
    Vector3 _last;

    void OnEnable() => _last = transform.position;

    void LateUpdate()
    {
        Vector3 delta = transform.position - _last;
        _last = transform.position;
        float dist = Vector3.Dot(delta, transform.forward);
        float deg = dist / Radius * Mathf.Rad2Deg;
        foreach (var w in Wheels) w.Rotate(Vector3.right, deg, Space.Self);
    }
}
