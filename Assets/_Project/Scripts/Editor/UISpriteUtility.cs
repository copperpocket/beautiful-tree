using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates a plain 8x8 white sprite for UI. Unity's built-in UISprite is a
/// 9-sliced rounded rect, which distorts badly under Image Type = Filled.
/// Tools > UI > Create White UI Sprite, or call GetOrCreate() from a builder.
/// </summary>
public static class UISpriteUtility
{
    public const string WhiteSpritePath = "Assets/_Project/Art/UI/T_White.png";

    [MenuItem("Tools/UI/Create White UI Sprite")]
    public static Sprite GetOrCreate()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath));

        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color32[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        Debug.Log($"Created {WhiteSpritePath}");
        return sprite;
    }
}
