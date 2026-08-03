#if UNITY_EDITOR
// SPEC 9.14.11 v3.274/v3.276/v3.277: create/rebuild LangRen_DZ DicedSpriteAtlas (Atlas Size Limit 1024)。
// v3.277：薄封装，核心构建委托 DicedAtlasBuilder；保留既有 Atlas 路径、菜单与自动补建。
using SpriteDicing;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    public static class LangRenDzDicedAtlasBuilder
    {
        private const string AtlasPath = "Assets/Art/Animations/WerewolfBackflip/LangRen_DZ_Atlas.asset";
        /// <summary>SPEC §9.14.11：LangRen_DZ 源帧目录（绿幕管线比对用）。</summary>
        public const string FramesSpriteFolder = "Assets/Resources/SpriteDicing/LangRen_DZ _1/frames_sprite";
        private const string InputFolderPath = FramesSpriteFolder;
        private const string DecoupledSpritesFolder = "Assets/Resources/SpriteDicing/LangRen_DZ _1/diced_sprites";
        private const string AutoBuildSessionKey = "PetDemo.LangRenDzAtlas.AutoBuildAttempted.v3.274b";

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
                    if (limitProp != null && limitProp.intValue != DicedAtlasBuilder.DefaultAtlasSizeLimit)
                        needsRebuildForLimit = true;
                }

                string[] existing = AssetDatabase.FindAssets("t:Sprite", new[] { DecoupledSpritesFolder });
                bool hasSprites = existing != null && existing.Length > 0;
                if (hasSprites && !needsRebuildForLimit)
                    return;

                Debug.Log(needsRebuildForLimit
                    ? "[LangRenDzDicedAtlasBuilder] Atlas Size Limit needs update to " + DicedAtlasBuilder.DefaultAtlasSizeLimit + ", auto Rebuild..."
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
            DicedAtlasBuilder.Build(InputFolderPath, DecoupledSpritesFolder, AtlasPath, "LangRenDzDicedAtlasBuilder");
        }
    }
}
#endif
