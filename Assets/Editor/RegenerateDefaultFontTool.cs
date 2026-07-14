using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public class RegenerateDefaultFontTool
{
    [MenuItem("Tools/Font/Change Default Font Source")]
    static void ChangeFontSource()
    {
        string newFontPath = EditorUtility.OpenFilePanel("Select new font file", "Assets", "ttf,otf");
        if (string.IsNullOrEmpty(newFontPath)) return;

        newFontPath = "Assets" + newFontPath.Substring(Application.dataPath.Length);
        Font newSourceFont = AssetDatabase.LoadAssetAtPath<Font>(newFontPath);

        string existingPath = "Assets/Fonts/DefaultFont.asset"; // adjust if needed
        TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(existingPath);

        if (newSourceFont == null || existingAsset == null)
        {
            Debug.LogError("Missing source font or DefaultFont.asset.");
            return;
        }

        TMP_FontAsset generated = TMP_FontAsset.CreateFontAsset(
            newSourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);

        if (generated == null)
        {
            Debug.LogError("Font generation failed.");
            return;
        }

        // Remove old sub-assets (old atlas textures + material) from the file
        Object[] oldSubAssets = AssetDatabase.LoadAllAssetsAtPath(existingPath);
        foreach (var sub in oldSubAssets)
        {
            if (sub is Texture2D || (sub is Material && sub != (Object)existingAsset))
                Object.DestroyImmediate(sub, true);
        }

        // Copy core font data (glyphs, character table, etc.)
        EditorUtility.CopySerialized(generated, existingAsset);
        existingAsset.name = "DefaultFont"; // restore main asset name to match filename

        // Persist the NEW atlas texture(s) as real sub-assets this time
        existingAsset.atlasTextures = generated.atlasTextures;
        foreach (var tex in existingAsset.atlasTextures)
        {
            tex.name = existingAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, existingAsset);
        }

        // Persist the NEW material and point it at the new atlas
        Material newMat = generated.material;
        newMat.name = existingAsset.name + " Material";
        newMat.SetTexture(ShaderUtilities.ID_MainTex, existingAsset.atlasTextures[0]);
        existingAsset.material = newMat;
        AssetDatabase.AddObjectToAsset(newMat, existingAsset);

        EditorUtility.SetDirty(existingAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Detach references so the temp object's OnDestroy doesn't try
        // to destroy the textures/material we just persisted into DefaultFont.asset
        generated.atlasTextures = new Texture2D[0];
        generated.material = null;
        Object.DestroyImmediate(generated);

        Debug.Log($"DefaultFont.asset updated in place with source: {newSourceFont.name}");
    }
}