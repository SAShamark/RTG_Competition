using UnityEngine;

public class LightBar : MonoBehaviour
{
    public Light Left, Right;
    public Renderer LeftLamp, RightLamp;
    public float Frequency = 3.5f;
    public float Intensity = 6f;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock _mpb;

    void Update()
    {
        _mpb ??= new MaterialPropertyBlock();
        // double flash per side, like real light bars
        float phase = Mathf.Repeat(Time.time * Frequency, 1f);
        bool leftOn = phase < 0.5f && Mathf.Repeat(phase * 4f, 1f) < 0.55f;
        bool rightOn = phase >= 0.5f && Mathf.Repeat(phase * 4f, 1f) < 0.55f;
        Set(Left, LeftLamp, leftOn, new Color(1f, 0.1f, 0.08f));
        Set(Right, RightLamp, rightOn, new Color(0.15f, 0.35f, 1f));
    }

    void Set(Light l, Renderer r, bool on, Color c)
    {
        l.intensity = on ? Intensity : 0f;
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionId, on ? c * 6f : c * 0.15f);
        r.SetPropertyBlock(_mpb);
    }
}
