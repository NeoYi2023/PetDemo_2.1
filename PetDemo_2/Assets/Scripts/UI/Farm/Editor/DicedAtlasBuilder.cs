#if UNITY_EDITOR
// SPEC §9.14.11 v3.277：frames_sprite → DicedSpriteAtlas 泛化构建器（任意 AnimName）。
using System.IO;
using SpriteDicing;
using SpriteDicing.Editors;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// 对任意 <c>Assets/Resources/SpriteDicing/{AnimName}/frames_sprite</c> 构建 DicedSpriteAtlas。
    /// Build 参数沿用 LangRen 约定（Trim=OFF / KeepPivot=ON / Unit=64 / Padding=2 / Limit=1024 / PPU=100 / Pivot=(0.5,0.5) / Decouple=ON）。
    /// </summary>
    public static class DicedAtlasBuilder
    {
        public const string ResourcesRoot = "Assets/Resources/SpriteDicing";
        public const string AtlasRoot = "Assets/Art/Animations";
        public const int DefaultAtlasSizeLimit = 1024;

        public static string GetFramesFolder(string animName)
        {
            return ResourcesRoot + "/" + animName + "/frames_sprite";
        }

        public static string GetDicedFolder(string animName)
        {
            return ResourcesRoot + "/" + animName + "/diced_sprites";
        }

        public static string GetAtlasDir(string animName)
        {
            return AtlasRoot + "/" + animName;
        }

        public static string GetAtlasPath(string animName)
        {
            return GetAtlasDir(animName) + "/" + animName + "_Atlas.asset";
        }

        /// <summary>按默认路径约定构建指定 AnimName 的 Atlas。返回生成的 sprite 数，失败返回 -1。</summary>
        public static int BuildForAnim(string animName)
        {
            if (string.IsNullOrWhiteSpace(animName))
            {
                Debug.LogError("[DicedAtlasBuilder] animName is empty.");
                return -1;
            }

            string name = animName.Trim();
            return Build(
                GetFramesFolder(name),
                GetDicedFolder(name),
                GetAtlasPath(name),
                "DicedAtlasBuilder");
        }

        /// <summary>核心构建：显式指定输入帧目录 / 解耦输出目录 / Atlas 资产路径。返回 sprite 数，失败 -1。</summary>
        public static int Build(string inputFolderPath, string decoupledSpritesFolder, string atlasPath, string logTag)
        {
            string atlasDir = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(atlasDir))
                EnsureFolder(atlasDir.Replace('\\', '/'));
            EnsureFolder(decoupledSpritesFolder);

            if (!AssetDatabase.IsValidFolder(inputFolderPath))
            {
                Debug.LogError("[" + logTag + "] Missing input folder: " + inputFolderPath);
                return -1;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<DicedSpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = ScriptableObject.CreateInstance<DicedSpriteAtlas>();
                AssetDatabase.CreateAsset(atlas, atlasPath);
                AssetDatabase.SaveAssets();
            }

            var inputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(inputFolderPath);
            if (inputFolder == null)
            {
                Debug.LogError("[" + logTag + "] Cannot load Input Folder: " + inputFolderPath);
                return -1;
            }

            string dicedGuid = AssetDatabase.AssetPathToGUID(decoupledSpritesFolder);
            if (string.IsNullOrEmpty(dicedGuid))
            {
                AssetDatabase.Refresh();
                dicedGuid = AssetDatabase.AssetPathToGUID(decoupledSpritesFolder);
            }

            if (string.IsNullOrEmpty(dicedGuid))
            {
                Debug.LogError("[" + logTag + "] Invalid diced_sprites folder GUID: " + decoupledSpritesFolder);
                return -1;
            }

            var so = new SerializedObject(atlas);
            EditorProperties.InitializeProperties(so);

            EditorProperties.TrimTransparentProperty.boolValue = false;
            EditorProperties.KeepOriginalPivotProperty.boolValue = true;
            EditorProperties.DefaultPivotProperty.vector2Value = new Vector2(0.5f, 0.5f);
            EditorProperties.UnitSizeProperty.intValue = 64;
            EditorProperties.PaddingProperty.intValue = 2;
            EditorProperties.AtlasSizeLimitProperty.intValue = DefaultAtlasSizeLimit;
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

            atlas = AssetDatabase.LoadAssetAtPath<DicedSpriteAtlas>(atlasPath);
            int spriteCount = atlas != null && atlas.Sprites != null ? atlas.Sprites.Count : 0;
            Debug.Log("[" + logTag + "] Build done: " + atlasPath + ", sprites=" + spriteCount + ", out=" + decoupledSpritesFolder);

            if (spriteCount < 1)
                Debug.LogWarning("[" + logTag + "] No sprites generated.");
            return spriteCount;
        }

        public static void EnsureFolder(string assetPath)
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
