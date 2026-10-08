using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds cast glows and the heal burst, adds AbilityVFX to the Player and
/// assigns them to AB_Mend and AB_Fireball. Tools > RPG > Build Cast Effects.
/// Safe to re-run.
/// </summary>
public static class CastEffectsBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";
    private const string MaterialFolder = "Assets/_Project/Art/Materials/VFX";
    private const string AbilityFolder = "Assets/_Project/ScriptableObjects/Abilities";

    private static readonly Color HealGreen = new Color(0.35f, 1f, 0.5f);
    private static readonly Color FireOrange = new Color(1f, 0.5f, 0.1f);

    [MenuItem("Tools/RPG/Build Cast Effects")]
    private static void Build()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        var healGlow = BuildHandGlow("PF_HealCastGlow", HealGreen);
        var fireGlow = BuildHandGlow("PF_FireCastGlow", FireOrange);
        var healBurst = BuildHealBurst("PF_HealBurst", HealGreen);

        var player = Object.FindFirstObjectByType<PlayerAbilities>();
        if (player == null)
        {
            Debug.LogWarning("No PlayerAbilities in the scene. Prefabs built, but add AbilityVFX to the Player yourself.");
        }
        else
        {
            var vfx = player.GetComponent<AbilityVFX>();
            if (vfx == null) vfx = Undo.AddComponent<AbilityVFX>(player.gameObject);

            SetEntry(vfx, "AB_Mend", healGlow, healBurst);
            SetEntry(vfx, "AB_Fireball", fireGlow, null);

            EditorUtility.SetDirty(vfx);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cast effects built. Save the scene to keep the AbilityVFX setup.");
    }

    // ---- Prefabs ----

    private static GameObject BuildHandGlow(string name, Color color)
    {
        var root = new GameObject(name);
        root.AddComponent<PulseGlow>();

        MakeSphere("Core", root.transform, 0.07f,
            SaveMaterial(name + "_Core", Color.Lerp(color, Color.white, 0.6f), additive: true));
        MakeSphere("Glow", root.transform, 0.18f,
            SaveMaterial(name + "_Glow", new Color(color.r, color.g, color.b, 0.45f), additive: true));
        AddLight(root.transform, color, intensity: 1.5f, range: 2f, Vector3.zero);

        return Save(root, name);
    }

    private static GameObject BuildHealBurst(string name, Color color)
    {
        var root = new GameObject(name);

        // Rising sparkles. The particle system destroys the prefab when it finishes.
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // point the cone upward

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetSphereMesh();
        renderer.sharedMaterial = SaveMaterial(name + "_Sparkle", Color.white, additive: true);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Body flash at chest height.
        var flash = new GameObject("Flash");
        flash.transform.SetParent(root.transform, false);
        flash.transform.localPosition = new Vector3(0f, 1f, 0f);
        var impact = flash.AddComponent<ImpactFlash>();
        impact.duration = 0.5f;
        impact.startScale = 0.6f;
        impact.endScale = 2.2f;
        MakeSphere("Shell", flash.transform, 1f,
            SaveMaterial(name + "_Shell", new Color(color.r, color.g, color.b, 0.35f), additive: true));
        AddLight(flash.transform, color, intensity: 3f, range: 4f, Vector3.zero);

        return Save(root, name);
    }

    private static void SetEntry(AbilityVFX vfx, string abilityName, GameObject castLoop, GameObject complete)
    {
        var ability = AssetDatabase.LoadAssetAtPath<Ability>($"{AbilityFolder}/{abilityName}.asset");
        if (ability == null)
        {
            Debug.LogWarning($"Couldn't find {abilityName}. Add its entry to AbilityVFX manually.");
            return;
        }

        var entry = vfx.entries.Find(e => e.ability == ability);
        if (entry == null)
        {
            entry = new AbilityVFX.Entry { ability = ability };
            vfx.entries.Add(entry);
        }

        entry.castLoop = castLoop;
        entry.completeEffect = complete;
    }

    // ---- Helpers ----

    private static GameObject Save(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Mesh GetSphereMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }

    private static void MakeSphere(string name, Transform parent, float size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private static void AddLight(Transform parent, Color color, float intensity, float range, Vector3 pos)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static Material SaveMaterial(string name, Color color, bool additive)
    {
        string path = $"{MaterialFolder}/M_{name}.mat";
        string shaderName = additive
            ? "Universal Render Pipeline/Particles/Unlit"
            : "Universal Render Pipeline/Unlit";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = Shader.Find(shaderName);
        }

        mat.SetColor("_BaseColor", color);

        if (additive)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
