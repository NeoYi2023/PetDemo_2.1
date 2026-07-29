#if UNITY_EDITOR
// SPEC 9.14.11 v3.274/v3.276: create/rebuild LangRen_DZ DicedSpriteAtlas (Atlas Size Limit 1024).
using System.IO;
using SpriteDicing;
using SpriteDicing.Editors;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    public static class LangRenDzDicedAtlasBuilder
    {
        private const string AtlasDir = "Assets/Art/Animations/WerewolfBackflip";
        private const string AtlasPath = AtlasDir + "/LangRen_DZ_Atlas.asset";
        /// <summary>SPEC §9.14.11：LangRen_DZ 源帧目录（绿幕管线比对用）。</summary>
        public const string FramesSpriteFolder = "Assets/Resources/SpriteDicing/LangRen_DZ _1/frames_sprite";
        private const string InputFolderPath = FramesSpriteFolder;
        private const string DecoupledSpritesFolder = "Assets/Resources/SpriteDicing/LangRen_DZ _1/diced_sprites";
        private const string AutoBuildSessionKey = "PetDemo.LangRenDzAtlas.AutoBuildAttempted.v3.274b";
        private const int DesiredAtlasSizeLimit = 1024;

        [InitializeOnLoadMethod]
        private static void AutoBuildOnceIfMissing()
        {
            if (SessionState.GetBool(AutoBuildSessionKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AutoBuildSessionKey, false))
                    return;
                SessionState.SetBool(AutoBuildSessionKey, true);

                if (!AssetDatabase.IsValidFolder(InputFolderPath))
                    return;

                var atlas = AssetDatabase.LoadAssetAtPath<DicedSpriteAtlas>(AtlasPath);
                bool needsRebuildForLimit = false;
                if (atlas != null)
                {
                    var so = new SerializedObject(atlas);
                    var limitProp = so.FindProperty("atlasSizeLimit");
                    if (limitProp != null && limitProp.intValue != DesiredAtlasSizeLimit)
                        needsRebuildForLimit = true;
                }

                string[] existing = AssetDatabase.FindAssets("t:Sprite", new[] { DecoupledSpritesFolder });
                bool hasSprites = existing != null && existing.Length > 0;
                if (hasSprites && !needsRebuildForLimit)
                    return;

                Debug.Log(needsRebuildForLimit
                    ? "[LangRenDzDicedAtlasBuilder] Atlas Size Limit needs update to " + DesiredAtlasSizeLimit + ", auto Rebuild..."
                    : "[LangRenDzDicedAtlasBuilder] diced_sprites empty, auto Build Atlas...");
                BuildInternal();
            };
        }

        [MenuItem("Tools/PetDemo/Build LangRen DZ Diced Atlas")]
        public static void Build()
        {
            BuildInternal();
        }

        public static void BuildFromCommandLine()
        {
            BuildInternal();
        }

        private static void BuildInternal()
        {
            EnsureFolder(AtlasDir);
            EnsureFolder(DecoupledSpritesFolder);

            if (!AssetDatabase.IsValidFolder(InputFolderPath))
            {
                Debug.LogError("[LangRenDzDicedAtlasBuilder] Missing input folder: " + InputFolderPath);
                return;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<DicedSpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = ScriptableObject.CreateInstance<DicedSpriteAtlas>();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
                AssetDatabase.SaveAssets();
            }

            var inputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(InputFolderPath);
            if (inputFolder == null)
            {
                Debug.LogError("[LangRenDzDicedAtlasBuilder] Cannot load Input Folder: " + InputFolderPath);
                return;
            }

            string dicedGuid = AssetDatabase.AssetPathToGUID(DecoupledSpritesFolder);
            if (string.IsNullOrEmpty(dicedGuid))
            {
                AssetDatabase.Refresh();
                dicedGuid = AssetDatabase.AssetPathToGUID(DecoupledSpritesFolder);
            }

            if (string.IsNullOrEmpty(dicedGuid))
            {
                Debug.LogError("[LangRenDzDicedAtlasBuilder] Invalid diced_sprites folder GUID: " + DecoupledSpritesFolder);
                return;
            }

            var so = new SerializedObject(atlas);
            EditorProperties.InitializeProperties(so);

            EditorProperties.TrimTransparentProperty.boolValue = false;
            EditorProperties.KeepOriginalPivotProperty.boolValue = true;
            EditorProperties.DefaultPivotProperty.vector2Value = new Vector2(0.5f, 0.5f);
            EditorProperties.UnitSizeProperty.intValue = 64;
            EditorProperties.PaddingProperty.intValue = 2;
            EditorProperties.AtlasSizeLimitProperty.intValue = DesiredAtlasSizeLimit;
            EditorProperties.PPUProperty.floatValue = 100f;
            EditorProperties.DecoupleSpriteDataProperty.boolValue = true;
            EditorProperties.InputFolderProperty.objectReferenceValue = inputFolder;
            EditorProperties.IncludeSubfoldersProperty.boolValue = false;
            EditorProperties.PrependSubfolderNamesProperty.boolValue = false;
            EditorProperties.GeneratedSpritesFolderGuidProperty.stringValue = dicedGuid;
            so.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                new AtlasBuilder(so).Build();
            }
            catch (ExitGUIException)
            {
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            atlas = AssetDatabase.LoadAssetAtPath<DicedSpriteAtlas>(AtlasPath);
            int spriteCount = atlas != null && atlas.Sprites != null ? atlas.Sprites.Count : 0;
            Debug.Log("[LangRenDzDicedAtlasBuilder] Build done: " + AtlasPath + ", sprites=" + spriteCount + ", out=" + DecoupledSpritesFolder);

            if (spriteCount < 1)
                Debug.LogWarning("[LangRenDzDicedAtlasBuilder] No sprites generated.");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (!Directory.Exists(abs))
                Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }
}
#endif
