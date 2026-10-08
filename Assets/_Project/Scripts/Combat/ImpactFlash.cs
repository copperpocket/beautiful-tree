using UnityEngine;

/// <summary>
/// A short burst: grows, fades, dims its light, then destroys itself.
/// Attach to an impact prefab root.
/// </summary>
public class ImpactFlash : MonoBehaviour
{
    public float duration = 0.3f;
    public float startScale = 0.3f;
    public float endScale = 1.6f;

    private Renderer[] renderers;
    private Light[] lights;
    private float[] lightStart;
    private Color[] baseColors;
    private MaterialPropertyBlock block;
    private float age;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        lights = GetComponentsInChildren<Light>();
        block = new MaterialPropertyBlock();

        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].sharedMaterial;
            baseColors[i] = mat != null && mat.HasProperty("_BaseColor")
                ? mat.GetColor("_BaseColor")
                : Color.white;
        }

        lightStart = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            lightStart[i] = lights[i].intensity;

        Apply(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        Apply(t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void Apply(float t)
    {
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = baseColors[i];
            c.a *= 1f - t;
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            renderers[i].SetPropertyBlock(block);
        }

        for (int i = 0; i < lights.Length; i++)
            lights[i].intensity = Mathf.Lerp(lightStart[i], 0f, t);
    }
}
