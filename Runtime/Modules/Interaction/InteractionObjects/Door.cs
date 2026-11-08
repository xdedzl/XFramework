using System;
using System.Collections;
using UnityEngine;

namespace XFramework.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/InteractionObjects/Door")]
    public class Door : MonoBehaviour, IKeyCodeInteractable
    {
        [Header("Door Parts")]
        [SerializeField] private Transform leftDoor;   // 左门 Transform
        [SerializeField] private Transform rightDoor;  // 右门 Transform

        [Header("Rotation Axis (Local)")]
        [Tooltip("左右门旋转轴（本地坐标）。默认 Y 轴。")]
        [SerializeField] private Vector3 leftAxis = Vector3.up;
        [SerializeField] private Vector3 rightAxis = Vector3.up;

        [Header("Angles (degrees)")]
        [Tooltip("左门关闭角度")]
        [SerializeField] private float leftClosedAngle = 0f;
        [Tooltip("左门打开角度")]
        [SerializeField] private float leftOpenAngle = 90f;

        [Tooltip("右门关闭角度")]
        [SerializeField] private float rightClosedAngle = 0f;
        [Tooltip("右门打开角度（常见为负角度以相反方向开启）")]
        [SerializeField] private float rightOpenAngle = -90f;

        [Header("Open Direction")]
        [Tooltip("开启后，会根据玩家位于门前还是门后自动翻转开门方向；关闭时逻辑保持不变。")]
        [SerializeField] private bool autoDetermineOpenDirection = true;

        [Header("Animation")]
        [Tooltip("开关门总时长（秒）")]
        [Min(0.01f)]
        [SerializeField] private float duration = 0.6f;

        [Header("Inspector Slider")]
        [Tooltip("门进度：0=完全关闭，1=完全打开。拖动时会立刻映射到旋转。")]
        [Range(0f, 1f)]
        [SerializeField] private float doorProgress = 0f;

        [Header("Audio")]
        [Tooltip("开门音效")]
        [SerializeField] private AudioClip openStartClip;
        [Tooltip("关门音效")]
        [SerializeField] private AudioClip closeStartClip;
        [Tooltip("音量 (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float audioVolume = 1f;

        // 内部状态
        private Coroutine animRoutine;
        private const float EPS = 0.0001f;
        private float currentOpenDirectionSign = 1f;
        private Transform playerTransformCache;
        private Camera mainCameraCache;
        
        public void Interact()
        {
            if (doorProgress >= 0.5f)
            {
                CloseDoor();
            }
            else
            {
                OpenDoor();
            }
        }
        
        public void OpenDoor()
        {
            PrepareOpenDirection();
            // 播放开门开始音效
            PlayOneShot(openStartClip);
            StartAnimateTo(1f);
        }
        
        public void CloseDoor()
        {
            // 播放关门开始音效
            PlayOneShot(closeStartClip);
            StartAnimateTo(0f);
        }
        
        private void OnValidate()
        {
            ApplyProgress(doorProgress);
        }

        private void Reset()
        {
            // 尝试自动关联左右门
            if (!leftDoor || !rightDoor)
            {
                var children = GetComponentsInChildren<Transform>();
                foreach (var t in children)
                {
                    if (t == this.transform) continue;
                    var nameLower = t.name.ToLowerInvariant();
                    if (!leftDoor && (nameLower.Contains("left") || nameLower.Contains("l_door") || nameLower.Contains("door_l")))
                    {
                        leftDoor = t;
                    }
                    else if (!rightDoor && (nameLower.Contains("right") || nameLower.Contains("r_door") || nameLower.Contains("door_r")))
                    {
                        rightDoor = t;
                    }
                }
            }
        }

        private void StartAnimateTo(float target)
        {
            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }
            animRoutine = StartCoroutine(AnimateTo(target));
        }

        private IEnumerator AnimateTo(float target)
        {
            float start = doorProgress;
            float time = 0f;

            // 避免 duration 太小或起止相同
            if (Mathf.Abs(target - start) < EPS || duration <= EPS)
            {
                doorProgress = target;
                ApplyProgress(doorProgress);
                animRoutine = null;
                yield break;
            }

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                // 可替换为更柔和的缓动，如 SmoothStep
                float eased = Mathf.SmoothStep(0f, 1f, t);
                doorProgress = Mathf.Lerp(start, target, eased);
                ApplyProgress(doorProgress);
                yield return null;
            }

            doorProgress = target;
            ApplyProgress(doorProgress);
            animRoutine = null;
        }

        // 将进度映射到左右门的旋转
        private void ApplyProgress(float progress)
        {
            // 计算目标角度
            float leftAngle = Mathf.Lerp(leftClosedAngle, leftOpenAngle * currentOpenDirectionSign, progress);
            float rightAngle = Mathf.Lerp(rightClosedAngle, rightOpenAngle * currentOpenDirectionSign, progress);

            // 应用到本地旋转（沿指定轴旋转）
            if (leftDoor)
            {
                leftDoor.localRotation = RotationAroundLocalAxis(leftDoor, leftAxis, leftAngle);
            }
            if (rightDoor)
            {
                rightDoor.localRotation = RotationAroundLocalAxis(rightDoor, rightAxis, rightAngle);
            }
        }

        // 根据本地轴生成旋转（避免直接改 euler 引发轴错乱）
        private static Quaternion RotationAroundLocalAxis(Transform t, Vector3 localAxis, float angleDeg)
        {
            // 单轴旋转的基准为单位四元数
            // 将本地轴标准化，并基于角度创建旋转
            Vector3 axis = localAxis.sqrMagnitude < EPS ? Vector3.up : localAxis.normalized;
            return Quaternion.AngleAxis(angleDeg, axis);
        }

        private void PrepareOpenDirection()
        {
            currentOpenDirectionSign = 1f;

            if (!autoDetermineOpenDirection)
            {
                return;
            }

            if (!TryGetInteractorPosition(out var interactorPosition))
            {
                return;
            }

            Vector3 localInteractorPosition = transform.InverseTransformPoint(interactorPosition);
            if (Mathf.Abs(localInteractorPosition.z) <= EPS)
            {
                return;
            }

            currentOpenDirectionSign = localInteractorPosition.z >= 0f ? 1f : -1f;
        }

        private bool TryGetInteractorPosition(out Vector3 interactorPosition)
        {
            ResolveRuntimeReferences();

            if (playerTransformCache)
            {
                interactorPosition = playerTransformCache.position;
                return true;
            }

            if (mainCameraCache)
            {
                interactorPosition = mainCameraCache.transform.position;
                return true;
            }

            interactorPosition = default;
            return false;
        }

        private void ResolveRuntimeReferences()
        {
            if (playerTransformCache == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                playerTransformCache = player ? player.transform : null;
            }

            if (mainCameraCache == null)
            {
                mainCameraCache = Camera.main;
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (!clip) return;
            
            SoundManager.Instance.PlaySound3D(clip, transform.position, volume: audioVolume);
        }
    }
}
