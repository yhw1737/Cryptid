using UnityEditor;
using UnityEngine;
using TMPro;

namespace Cryptid.Editor
{
    /// <summary>
    /// Editor utility to generate a dynamic TMP FontAsset from NotoSansKR-Regular.ttf.
    /// The generated asset is placed in Assets/Resources/Fonts/ so UIFactory can load it at runtime.
    ///
    /// Usage: Unity Menu → Tools → Cryptid → Generate Korean Font Asset
    /// Also auto-generates on editor load if the asset is missing.
    /// </summary>
    [InitializeOnLoad]
    public static class FontAssetGenerator
    {
        static FontAssetGenerator()
        {
            // Auto-generate Korean font asset on editor load if missing
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OUTPUT_PATH) == null)
                {
                    var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SOURCE_FONT_PATH);
                    if (sourceFont != null)
                    {
                        Debug.Log("[FontAssetGenerator] Korean font asset missing — auto-generating...");
                        GenerateKoreanFont();
                    }
                    else
                    {
                        Debug.LogWarning($"[FontAssetGenerator] Source font not found at {SOURCE_FONT_PATH}. " +
                            "Cannot auto-generate Korean font asset.");
                    }
                }
            };
        }

        private const string SOURCE_FONT_PATH = "Assets/NotoSansKR-Regular.ttf";
        private const string OUTPUT_DIR = "Assets/Resources/Fonts";
        private const string OUTPUT_PATH = "Assets/Resources/Fonts/NotoSansKR-Regular SDF.asset";

        [MenuItem("Tools/Cryptid/Generate Korean Font Asset")]
        public static void GenerateKoreanFont()
        {
            // Load the source TTF font
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SOURCE_FONT_PATH);
            if (sourceFont == null)
            {
                Debug.LogError($"[FontAssetGenerator] Source font not found at {SOURCE_FONT_PATH}");
                EditorUtility.DisplayDialog("Error",
                    $"Font not found at:\n{SOURCE_FONT_PATH}", "OK");
                return;
            }

            // Ensure output directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

            // Create a Dynamic TMP_FontAsset
            // Dynamic fonts render glyphs on demand — ideal for CJK character sets
            // which have too many glyphs for a static atlas
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,          // Sampling point size
                9,           // Padding
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,        // Atlas width
                1024         // Atlas height
            );

            if (fontAsset == null)
            {
                Debug.LogError("[FontAssetGenerator] Failed to create TMP_FontAsset!");
                return;
            }

            fontAsset.name = "NotoSansKR-Regular SDF";

            // Set as multi-atlas so new glyphs can be added at runtime
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // Save asset
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OUTPUT_PATH);
            if (existing != null)
            {
                EditorUtility.CopySerialized(fontAsset, existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"[FontAssetGenerator] Updated existing font asset at {OUTPUT_PATH}");
            }
            else
            {
                AssetDatabase.CreateAsset(fontAsset, OUTPUT_PATH);

                // Save atlas textures as sub-assets (required for Dynamic TMP fonts)
                if (fontAsset.atlasTextures != null)
                {
                    foreach (var tex in fontAsset.atlasTextures)
                    {
                        if (tex != null && !AssetDatabase.IsSubAsset(tex))
                            AssetDatabase.AddObjectToAsset(tex, OUTPUT_PATH);
                    }
                }

                // Save material as sub-asset
                if (fontAsset.material != null && !AssetDatabase.IsSubAsset(fontAsset.material))
                    AssetDatabase.AddObjectToAsset(fontAsset.material, OUTPUT_PATH);

                AssetDatabase.SaveAssets();
                Debug.Log($"[FontAssetGenerator] Created font asset at {OUTPUT_PATH}");
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success",
                $"Korean font asset generated!\n\n{OUTPUT_PATH}\n\n" +
                "The font uses Dynamic atlas mode — glyphs are rendered on demand.\n" +
                "UIFactory will auto-load it via Resources.Load.", "OK");
        }
    }
}
