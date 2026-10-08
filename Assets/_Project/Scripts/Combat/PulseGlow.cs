using UnityEngine;

/// <summary>
/// Fades in, then gently pulses size and light. Used for looping cast glows.
/// Attach to the glow prefab root.
/// </summary>
public class PulseGlow : MonoBehaviour
{
    public float fadeIn = 0.2f;
    public float pulseSpeed = 6f;
    [Range(0f, 1f)] public float pulseAmount = 0.2f;

    private Vector3 baseScale;
    private Light[] lights;
    private float[] baseIntensity;
    private float age;

    void Awake()
    {
        baseScale = transform.localScale;
        lights = GetComponentsInChildren<Light>();
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            baseIntensity[i] = lights[i].intensity;

        Apply(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        Apply(Mathf.Clamp01(age / fadeIn));
    }

    private void Apply(float fade)
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse * fade;

        for (int i = 0; i < lights.Length; i++)
            lights[i].intensity = baseIntensity[i] * pulse * fade;
    }
}
