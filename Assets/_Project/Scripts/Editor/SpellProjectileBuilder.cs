using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds Fireball and Frost Bolt projectile and impact prefabs, then assigns
/// them to AB_Fireball and AB_Frostbolt. Tools > RPG > Build Spell Projectiles.
/// Safe to re-run: it overwrites the prefabs and materials.
/// </summary>
public static class SpellProjectileBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";
    private const string MaterialFolder = "Assets/_Project/Art/Materials/VFX";
    private const string AbilityFolder = "Assets/_Project/ScriptableObjects/Abilities";

    [MenuItem("Tools/RPG/Build Spell Projectiles")]
    private static void Build()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        var fireImpact = BuildImpact("PF_FireImpact", new Color(1f, 0.55f, 0.1f));
        var frostImpact = BuildImpact("PF_FrostImpact", new Color(0.45f, 0.8f, 1f));

        var fireball = BuildProjectile("PF_Fireball",
            core: new Color(1f, 0.85f, 0.4f), glow: new Color(1f, 0.4f, 0.05f),
            coreSize: 0.28f, impact: fireImpact);

        var frostbolt = BuildProjectile("PF_Frostbolt",
            core: new Color(0.85f, 0.95f, 1f), glow: new Color(0.3f, 0.65f, 1f),
            coreSize: 0.2f, impact: frostImpact);

        Assign("AB_Fireball", fireball, 16f);
        Assign("AB_Frostbolt", frostbolt, 24f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Spell projectiles built and assigned.");
    }

    // ---- Prefabs ----

    private static Projectile BuildProjectile(string name, Color core, Color glow, float coreSize, GameObject impact)
    {
        var root = new GameObject(name);
        var projectile = root.AddComponent<Projectile>();
        projectile.impactPrefab = impact;

        // Bright solid core.
        var coreObj = MakeSphere("Core", root.transform, coreSize,
            SaveMaterial(name + "_Core", core, additive: false));

        // Soft additive halo around it.
        MakeSphere("Glow", root.transform, coreSize * 2f,
            SaveMaterial(name + "_Glow", new Color(glow.r, glow.g, glow.b, 0.5f), additive: true));

        // Trail on its own child, so it can be detached and fade out on impact.
        var trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(root.transform, false);
        var trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = coreSize * 1.4f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = SaveMaterial(name + "_Trail", glow, additive: true);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(glow, 0f), new GradientColorKey(glow, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        AddLight(root.transform, glow, intensity: 2f, range: 4f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Projectile>();
    }

    private static GameObject BuildImpact(string name, Color color)
    {
        var root = new GameObject(name);
        root.AddComponent<ImpactFlash>();

        MakeSphere("Burst", root.transform, 1f,
            SaveMaterial(name + "_Burst", new Color(color.r, color.g, color.b, 0.8f), additive: true));
        AddLight(root.transform, color, intensity: 4f, range: 5f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void Assign(string abilityName, Projectile prefab, float speed)
    {
        var ability = AssetDatabase.LoadAssetAtPath<DamageAbility>($"{AbilityFolder}/{abilityName}.asset");
        if (ability == null)
        {
            Debug.LogWarning($"Couldn't find {abilityName}. Assign {prefab.name} to it manually.");
            return;
        }

        ability.projectilePrefab = prefab;
        ability.projectileSpeed = speed;
        EditorUtility.SetDirty(ability);   // without this the change isn't saved to disk
    }

    // ---- Helpers ----

    private static GameObject MakeSphere(string name, Transform parent, float size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(go.GetComponent<Collider>());   // must not block clicks or physics
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    private static void AddLight(Transform parent, Color color, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
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
            // Transparent, additive blending so it glows instead of looking solid.
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
