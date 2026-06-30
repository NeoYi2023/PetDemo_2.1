// SPEC §12.3 / §12.8 / §12.9：入侵战斗胜负结算弹窗（预制体视图）。
using System.Collections.Generic;
using PetDemo.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    /// <summary>
    /// 挂在 <c>Resources/Prefabs/Battle/InvasionBattleResultDialog.prefab</c> 根节点上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InvasionBattleResultDialogView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Battle/InvasionBattleResultDialog";

        /// <summary>暂时关闭结算弹窗内奖励列表展示；改回 <c>true</c> 即可恢复。</summary>
        private const bool RewardListEnabled = false;

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform rewardListRoot;
        [SerializeField] private InvasionBattleRewardRowView rewardRowTemplate;
        [SerializeField] private Toggle autoAdvanceToggle;
        [SerializeField] private Button closePanelButton;

        private readonly List<InvasionBattleRewardRowView> activeRewardRows = new List<InvasionBattleRewardRowView>();

        public RectTransform RootRt => transform as RectTransform;
        public Text TitleText => titleText;
        public Toggle AutoAdvanceToggle => autoAdvanceToggle;
        public Button ClosePanelButton => closePanelButton;

        private void Awake()
        {
            WireReferencesIfNeeded();
        }

        private void WireReferencesIfNeeded()
        {
            if (titleText == null)
                titleText = transform.Find("ResultText")?.GetComponent<Text>();
            if (rewardListRoot == null)
            {
                var reward = transform.Find("RewardList");
                if (reward != null)
                    rewardListRoot = reward as RectTransform;
            }
            if (rewardRowTemplate == null && rewardListRoot != null)
            {
                var row = rewardListRoot.Find("RewardRow");
                if (row != null)
                    rewardRowTemplate = row.GetComponent<InvasionBattleRewardRowView>();
            }
            if (autoAdvanceToggle == null)
            {
                var row = transform.Find("AutoAdvanceWinRow");
                if (row != null)
                    autoAdvanceToggle = row.GetComponent<Toggle>();
            }
            if (closePanelButton == null)
                closePanelButton = GetComponent<Button>();
            if (rewardRowTemplate != null)
                rewardRowTemplate.WireReferencesIfNeeded();

            if (!RewardListEnabled && rewardListRoot != null)
                rewardListRoot.gameObject.SetActive(false);
        }

        public void SetTitle(string text)
        {
            if (titleText != null)
                titleText.text = text ?? string.Empty;
        }

        public void SetAutoAdvanceRowVisible(bool visible)
        {
            if (autoAdvanceToggle != null)
                autoAdvanceToggle.gameObject.SetActive(visible);
        }

        public void RebuildRewards(bool playerWon, InvasionService service)
        {
            ClearRewardRows();
            if (rewardListRoot == null)
                return;
            if (!RewardListEnabled)
            {
                rewardListRoot.gameObject.SetActive(false);
                return;
            }
            if (!playerWon)
            {
                rewardListRoot.gameObject.SetActive(false);
                return;
            }

            var rewards = service != null ? service.GetVictoryRewards() : null;
            if (rewards == null || rewards.Count == 0 || rewardRowTemplate == null)
            {
                rewardListRoot.gameObject.SetActive(false);
                return;
            }

            rewardListRoot.gameObject.SetActive(true);
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.count <= 0 || string.IsNullOrEmpty(reward.id))
                    continue;

                var row = Instantiate(rewardRowTemplate, rewardListRoot);
                var rowLayout = row.GetComponent<LayoutElement>();
                if (rowLayout != null)
                    rowLayout.ignoreLayout = false;
                row.WireReferencesIfNeeded();
                row.Bind(reward);
                activeRewardRows.Add(row);
            }

            if (activeRewardRows.Count == 0)
                rewardListRoot.gameObject.SetActive(false);
        }

        public void ClearRewardRows()
        {
            for (int i = 0; i < activeRewardRows.Count; i++)
            {
                if (activeRewardRows[i] != null)
                    Destroy(activeRewardRows[i].gameObject);
            }
            activeRewardRows.Clear();
        }
    }
}
