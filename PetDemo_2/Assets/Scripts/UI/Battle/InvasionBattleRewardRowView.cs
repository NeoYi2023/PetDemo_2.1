// SPEC §12.8：入侵战斗胜利奖励单行（Icon + Count），布局由预制体定义。
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    /// <summary>
    /// 挂在 <c>RewardList/RewardRow</c> 模板节点上；运行时按掉落数据克隆并绑定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InvasionBattleRewardRowView : MonoBehaviour
    {
        private const string ResFertilizerIcon = "AirUI/ShiFei-1";
        private const string ResSeedPackIcon = "AirUI/item_1340000";

        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;

        private static Dictionary<string, PlantConfig> plantConfigById;
        private static Sprite fallbackWhiteSprite;

        public void WireReferencesIfNeeded()
        {
            if (iconImage == null)
                iconImage = transform.Find("Icon")?.GetComponent<Image>();
            if (countText == null)
                countText = transform.Find("Count")?.GetComponent<Text>();
        }

        public void Bind(InvasionRewardConfig reward)
        {
            WireReferencesIfNeeded();
            if (reward == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = ResolveRewardIcon(reward);
                iconImage.color = ResolveRewardIconColor(reward);
                iconImage.preserveAspect = true;
            }

            if (countText != null)
                countText.text = "x " + reward.count;

            gameObject.SetActive(true);
        }

        private static Sprite ResolveRewardIcon(InvasionRewardConfig reward)
        {
            if (reward == null)
                return LoadBuiltinUiSprite();

            switch (reward.kind)
            {
                case InvasionRewardKind.Seed:
                    return ResolveSeedIcon(reward.id);
                case InvasionRewardKind.Fertilizer:
                    return Resources.Load<Sprite>(ResFertilizerIcon) ?? LoadBuiltinUiSprite();
                case InvasionRewardKind.SeedPack:
                    return Resources.Load<Sprite>(ResSeedPackIcon) ?? LoadBuiltinUiSprite();
                default:
                    return LoadBuiltinUiSprite();
            }
        }

        private static Color ResolveRewardIconColor(InvasionRewardConfig reward)
        {
            if (reward == null || reward.kind != InvasionRewardKind.SeedPack)
                return Color.white;
            if (!System.Enum.TryParse<SeedPackQuality>(reward.id, false, out var quality))
                return Color.white;
            switch (quality)
            {
                case SeedPackQuality.Common: return HexToColor("#F5F5F5");
                case SeedPackQuality.Rare: return HexToColor("#4FA3FF");
                case SeedPackQuality.Epic: return HexToColor("#A26CFF");
                case SeedPackQuality.Legendary: return HexToColor("#FF9F40");
                default: return Color.white;
            }
        }

        private static Sprite ResolveSeedIcon(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId))
                return LoadBuiltinUiSprite();
            if (plantConfigById == null)
            {
                plantConfigById = new Dictionary<string, PlantConfig>();
                var configs = PlantConfigCatalog.LoadPlantConfigsFromCsv();
                for (int i = 0; i < configs.Count; i++)
                {
                    var cfg = configs[i];
                    if (cfg != null && !string.IsNullOrEmpty(cfg.id) && !plantConfigById.ContainsKey(cfg.id))
                        plantConfigById[cfg.id] = cfg;
                }
            }
            if (plantConfigById.TryGetValue(plantConfigId, out var config)
                && config != null
                && config.appearanceSpriteIds != null
                && config.appearanceSpriteIds.Count > 0
                && !string.IsNullOrEmpty(config.appearanceSpriteIds[0]))
            {
                var sprite = Resources.Load<Sprite>(config.appearanceSpriteIds[0]);
                if (sprite != null)
                    return sprite;
            }
            return LoadBuiltinUiSprite();
        }

        private static Sprite LoadBuiltinUiSprite()
        {
            if (fallbackWhiteSprite != null)
                return fallbackWhiteSprite;
            var tex = Texture2D.whiteTexture;
            if (tex == null)
                return null;
            fallbackWhiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return fallbackWhiteSprite;
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return Color.white;
        }
    }
}
