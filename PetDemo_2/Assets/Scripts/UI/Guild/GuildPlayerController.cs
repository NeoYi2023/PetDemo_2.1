// SPEC §9.8.9.4 / §9.8.9.6 ①：公会场景主角移动 — 摇杆方向 * moveSpeed * deltaTime，
// 分轴（先 X 后 Y）与 GuildObstacleArea 矩形 AABB 检测实现阻挡 + 贴墙滑动，
// 钳位在 GongHuiWorldContent 边界内；位移后同帧同步镜头（v3.178）。
using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 420f;
        [SerializeField] private Vector2 collisionBoxSize = new Vector2(140f, 100f);
        [SerializeField] private Vector2 collisionBoxOffset = new Vector2(0f, -60f);
        [SerializeField] private string idleAnimationName = "exclusive_2";
        [SerializeField] private string moveAnimationName = "move_1";

        private RectTransform playerRt;
        private RectTransform worldContentRt;
        private VirtualJoystickView joystick;
        private SkeletonGraphic skeletonGraphic;
        private readonly List<Rect> obstacleRects = new List<Rect>();
        private bool initialized;
        private bool moving;
        private bool animationLocked;
        private bool movementEnabled = true;
        private Action<Vector2> onMoved;

        public RectTransform PlayerRt => playerRt;
        public SkeletonGraphic PlayerSkeleton => skeletonGraphic;
        public bool IsAnimationLocked => animationLocked;

        public void SetAnimationLocked(bool locked)
        {
            animationLocked = locked;
        }

        /// <summary>SPEC §9.8.9.12：全景模式等场景下禁用主角移动。</summary>
        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!movementEnabled)
            {
                moving = false;
                if (!animationLocked)
                    PlayIdle();
            }
        }

        /// <summary>主角位移后同帧回调（content 局部 delta，用于镜头反向平移，v3.179）。</summary>
        public void BindViewportFollowSync(Action<Vector2> sync)
        {
            onMoved = sync;
        }

        /// <summary>work_2 等单次动作结束后，按当前移动状态恢复 idle/move 轨。</summary>
        public void RefreshAnimation()
        {
            if (!initialized || animationLocked)
                return;

            if (moving)
                PlayMove();
            else
                PlayIdle();
        }

        public void Initialize(
            RectTransform player,
            RectTransform worldContent,
            VirtualJoystickView joystickView,
            SkeletonGraphic playerSkeleton,
            IList<GuildObstacleArea> obstacles)
        {
            playerRt = player;
            worldContentRt = worldContent;
            joystick = joystickView;
            skeletonGraphic = playerSkeleton;

            obstacleRects.Clear();
            if (obstacles != null)
            {
                for (int i = 0; i < obstacles.Count; i++)
                {
                    if (obstacles[i] != null)
                        obstacleRects.Add(GuildSceneGeometry.RectInContentSpace(
                            obstacles[i].Rt, worldContentRt));
                }
            }

            initialized = true;
            moving = false;
            PlayIdle();
        }

        private void Update()
        {
            if (!initialized || !movementEnabled || playerRt == null || joystick == null)
                return;

            var dir = joystick.Direction;
            bool wantMove = dir.sqrMagnitude > 0.0001f;

            if (wantMove)
            {
                var delta = dir * (moveSpeed * Time.deltaTime);
                var pos = playerRt.anchoredPosition;
                var prevPos = pos;
                pos = TryMoveAxis(pos, new Vector2(delta.x, 0f));
                pos = TryMoveAxis(pos, new Vector2(0f, delta.y));
                pos = ClampToWorld(pos);
                playerRt.anchoredPosition = pos;
                onMoved?.Invoke(pos - prevPos);

                if (Mathf.Abs(dir.x) > 0.01f)
                    GuildSpineCharacterBuilder.SetFacing(playerRt, dir.x > 0f);
            }

            if (wantMove != moving)
            {
                moving = wantMove;
                if (!animationLocked)
                {
                    if (moving)
                        PlayMove();
                    else
                        PlayIdle();
                }
            }
        }

        // 单轴尝试位移：候选脚底盒与任一障碍矩形相交则维持原位（另一轴不受影响 → 贴墙滑动）。
        private Vector2 TryMoveAxis(Vector2 pos, Vector2 axisDelta)
        {
            if (axisDelta.sqrMagnitude <= 0f)
                return pos;

            var candidate = pos + axisDelta;
            var box = GuildSceneGeometry.CenteredRect(candidate + collisionBoxOffset, collisionBoxSize);
            for (int i = 0; i < obstacleRects.Count; i++)
            {
                if (box.Overlaps(obstacleRects[i]))
                    return pos;
            }

            return candidate;
        }

        private Vector2 ClampToWorld(Vector2 pos)
        {
            if (worldContentRt == null)
                return pos;

            var half = worldContentRt.rect.size * 0.5f;
            var halfBox = collisionBoxSize * 0.5f;
            var boxCenter = pos + collisionBoxOffset;
            boxCenter.x = Mathf.Clamp(boxCenter.x, -half.x + halfBox.x, half.x - halfBox.x);
            boxCenter.y = Mathf.Clamp(boxCenter.y, -half.y + halfBox.y, half.y - halfBox.y);
            return boxCenter - collisionBoxOffset;
        }

        private void PlayMove()
        {
            GuildSpineCharacterBuilder.PlayLoop(skeletonGraphic, moveAnimationName, "move", "animation");
        }

        private void PlayIdle()
        {
            GuildSpineCharacterBuilder.PlayLoop(
                skeletonGraphic, idleAnimationName, "standby_1", "animation", "idle");
        }
    }
}
