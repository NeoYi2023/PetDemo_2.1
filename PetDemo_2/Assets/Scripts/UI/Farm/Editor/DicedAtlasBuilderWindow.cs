#if UNITY_EDITOR
// SPEC §9.14.11 v3.277：frames_sprite → DicedSpriteAtlas 转图集窗口（任意 AnimName）。
using System;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    public sealed class DicedAtlasBuilderWindow : EditorWindow
    {
        private const string PrefAnimName = "PetDemo.DicedAtlasBuilder.AnimName";
        private const string DefaultAnimName = "LangRen_DZ _1";

        private string _animName = DefaultAnimName;
        private string _status = "";

        [MenuItem("Tools/PetDemo/Build Diced Atlas (frames_sprite → Atlas)")]
        public static void Open()
        {
            var window = GetWindow<DicedAtlasBuilderWindow>(true, "Diced Atlas Builder", true);
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        private void OnEnable()
        {
            _animName = EditorPrefs.GetString(PrefAnimName, DefaultAnimName);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("frames_sprite → DicedSpriteAtlas（转图集）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "对任意 AnimName 构建 DicedSpriteAtlas（SpriteDicing legacy v1，本地包）。\n" +
                "输入：Assets/Resources/SpriteDicing/{AnimName}/frames_sprite/\n" +
                "解耦输出：…/{AnimName}/diced_sprites/；Atlas：Assets/Art/Animations/{AnimName}/",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();
            _animName = EditorGUILayout.TextField("动画名 AnimName", _animName);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefAnimName, _animName ?? "");

            string name = string.IsNullOrWhiteSpace(_animName) ? DefaultAnimName : _animName.Trim();
            string framesFolder = DicedAtlasBuilder.GetFramesFolder(name);
            string dicedFolder = DicedAtlasBuilder.GetDicedFolder(name);
            string atlasPath = DicedAtlasBuilder.GetAtlasPath(name);

            EditorGUILayout.LabelField("输入目录", framesFolder);
            EditorGUILayout.LabelField("解耦输出", dicedFolder);
            EditorGUILayout.LabelField("Atlas 资产", atlasPath);

            int frameCount = CountSourceFrames(framesFolder);
            EditorGUILayout.LabelField("源帧数（已导入 Sprite）", frameCount.ToString());

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(frameCount < 1))
            {
                if (GUILayout.Button("Build（构建 Diced Atlas）", GUILayout.Height(32)))
                    Build(name, frameCount);
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Build 参数（沿用 LangRen 约定）：Trim Transparent=OFF、Keep Original Pivot=ON、Dice Unit Size=64、" +
                "Padding=2、Atlas Size Limit=1024、Pixels Per Unit=100、Default Pivot=(0.5,0.5)、Decouple Sprite Data=ON。" +
                "不解析 CLI sprites.json / atlas_*.png。",
                EditorStyles.wordWrappedLabel);
        }

        private void Build(string animName, int frameCount)
        {
            _status = "";
            try
            {
                EditorUtility.DisplayProgressBar("Diced Atlas Builder", "构建中…", 0.3f);
                int sprites = DicedAtlasBuilder.BuildForAnim(animName);
                if (sprites < 0)
                {
                    _status = "失败：构建未成功，详见 Console。";
                    EditorUtility.DisplayDialog("Diced Atlas Builder", _status, "OK");
                    return;
                }

                _status = "完成：" + animName + "，源帧 " + frameCount + "，生成 sprites=" + sprites
                          + " → " + DicedAtlasBuilder.GetDicedFolder(animName);
                EditorUtility.DisplayDialog("Diced Atlas Builder", _status, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _status = "异常：" + ex.Message;
                EditorUtility.DisplayDialog("Diced Atlas Builder", _status, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static int CountSourceFrames(string framesFolder)
        {
            if (!AssetDatabase.IsValidFolder(framesFolder))
                return 0;
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { framesFolder });
            return guids != null ? guids.Length : 0;
        }
    }
}
#endif
