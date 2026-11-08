using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace XFramework.Interaction
{
        // 监听键盘输入、管理候选对象并控制唯一高亮
        [DisallowMultipleComponent]
        public class InteractionManager : MonoSingleton<InteractionManager>
        {
            /// <summary>
            /// 交互输入总开关；项目层根据自身状态（如 GameLock、死亡、调试锁定）设置。
            /// </summary>
            public static bool IsInteractionInputEnabled { get; private set; } = true;

            public static void SetInteractionInputEnabled(bool enabled)
            {
                IsInteractionInputEnabled = enabled;
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetInteractionInputEnabled()
            {
                IsInteractionInputEnabled = true;
            }

            // 当前在交互范围内的对象集合（由触发器维护）
            private readonly List<InteractionObject> _candidates = new();
            private readonly List<Vector3> _candidatePositions = new();
            
            private readonly Dictionary<Type, IInteractionHandler> m_Type2Handlers = new();
            private readonly HashSet<string> m_InteractionInvisibleReasons = new(StringComparer.Ordinal);

            // 当前激活的处理器
            private IInteractionHandler m_CurrentHandler;
            private bool m_IsInteractionEnabled = true;

            public bool IsInteractionEnabled => m_IsInteractionEnabled;
            public bool IsInteractionVisible => m_InteractionInvisibleReasons.Count == 0;
            public IReadOnlyCollection<string> InteractionInvisibleReasons => m_InteractionInvisibleReasons;

            // 编辑器 Debug 只读数据
            public IReadOnlyList<InteractionObject> Candidates => _candidates;
            public IInteractionHandler CurrentHandler => m_CurrentHandler;
            public InteractionObject CurrentTarget => m_CurrentHandler?.GetTarget();
            public IReadOnlyDictionary<Type, IInteractionHandler> HandlerMap => m_Type2Handlers;

            [Header("Target Selection")]
            [SerializeField, Range(0f, 180f)] private float preferredHalfAngle = 65f;
            [SerializeField, Range(0f, 1f)] private float angleWeight = 0.65f;
            [SerializeField, Range(0f, 1f)] private float distanceWeight = 0.35f;
            [SerializeField, Tooltip("玩家离开交互对象触发范围后的额外容差（米），用于兜底清理瞬移/传送等未触发 OnTriggerExit 导致的残留候选")]
            private float interactionRangeTolerance = 0.5f;

            private Camera _cam;
            private Transform _playerTransform;

            private void OnEnable()
            {
                ResolveRuntimeReferences();
            }

            private void Awake()
            {
                InitInteractionHandlers();
            }

            private void InitInteractionHandlers()
            {
                if (m_Type2Handlers.Count > 0)
                {
                    return;
                }

                m_Type2Handlers.Clear();
                var handlerTypes = Utility.Reflection.GetAssignableTypes(typeof(IInteractionHandler),
                    "XFrameworkRuntime", "Assembly-CSharp");
                foreach (var handlerType in handlerTypes)
                {
                    var targetInteractable = handlerType.GetCustomAttribute<TargetInteractableAttribute>();
                    if(targetInteractable?.interactableType == null)
                    {
                        throw new Exception($"InteractionManager: InteractionHandler {handlerType.Name} missing TargetInteractableAttribute.");
                    }

                    var handler = Utility.Reflection.CreateInstance<IInteractionHandler>(handlerType);
                    handler.SetVisible(IsInteractionVisible);


                    var assignableTypes = Utility.Reflection.GetAssignableTypes(targetInteractable.interactableType);
                    foreach (var assignableType in assignableTypes)
                    {
                        m_Type2Handlers[assignableType] = handler;
                    }
                }
            }

            private void Update()
            {
                if (!m_IsInteractionEnabled)
                {
                    return;
                }

                ResolveRuntimeReferences();
                SelectBestCandidate();

                m_CurrentHandler?.OnUpdate();
            }

            public bool SetInteractionEnabled(bool enabled)
            {
                if (m_IsInteractionEnabled == enabled)
                {
                    return false;
                }

                m_IsInteractionEnabled = enabled;
                if (!m_IsInteractionEnabled)
                {
                    SetCurrent(null);
                }

                return true;
            }

            public bool SetInteractionVisible(string reason, bool visible)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("Interaction visibility reason cannot be null or whitespace.", nameof(reason));
                }

                bool wasVisible = IsInteractionVisible;
                if (visible)
                {
                    m_InteractionInvisibleReasons.Remove(reason);
                }
                else
                {
                    m_InteractionInvisibleReasons.Add(reason);
                }

                bool isVisible = IsInteractionVisible;
                if (wasVisible == isVisible)
                {
                    return false;
                }

                var handlers = new HashSet<IInteractionHandler>(m_Type2Handlers.Values);
                foreach (IInteractionHandler handler in handlers)
                {
                    handler.SetVisible(isVisible);
                }

                return true;
            }

            // 供 InteractionObject 调用
            internal void Register(InteractionObject obj)
            {
                if (obj == null) return;
                if (!_candidates.Contains(obj))
                {
                    _candidates.Add(obj);
                }
            }

            internal void Unregister(InteractionObject obj)
            {
                if (obj == null) return;
                if (_candidates.Remove(obj))
                {
                    // 如果当前选中被移除，立刻清理并重新选择
                    if (m_CurrentHandler?.GetTarget() == obj)
                    {
                        SetCurrent(null);
                    }
                }
            }

            private void ResolveRuntimeReferences()
            {
                if (_cam == null)
                {
                    _cam = Camera.main;
                }

                if (_playerTransform == null)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    _playerTransform = player ? player.transform : null;
                }
            }

            private void SelectBestCandidate()
            {
                if (!TryGetSelectionContext(out var playerPosition, out var cameraForward, out var playerForward))
                {
                    SetCurrent(null);
                    return;
                }

                PurgeOutOfRangeCandidates(playerPosition);
                RebuildCandidatePositions();

                if (_candidatePositions.Count == 0)
                {
                    SetCurrent(null);
                    return;
                }

                int bestIndex = SelectBestCandidateIndex(_candidatePositions, playerPosition, cameraForward, playerForward,
                    preferredHalfAngle, distanceWeight, angleWeight);
                SetCurrent(bestIndex >= 0 ? _candidates[bestIndex] : null);
            }

            // 兜底清理：瞬移/传送等场景下物理 trigger 可能不触发 OnTriggerExit，
            // 导致候选对象残留。以候选自身的触发范围为界，玩家已离开则视为失效并注销。
            private void PurgeOutOfRangeCandidates(Vector3 playerPosition)
            {
                for (int i = _candidates.Count - 1; i >= 0; i--)
                {
                    InteractionObject candidate = _candidates[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    Collider col = candidate.GetComponent<Collider>();
                    if (col == null)
                    {
                        continue;
                    }

                    Bounds bounds = col.bounds;
                    bounds.Expand(interactionRangeTolerance);
                    if (!bounds.Contains(playerPosition))
                    {
                        Unregister(candidate);
                    }
                }
            }

            private void RebuildCandidatePositions()
            {
                for (int i = _candidates.Count - 1; i >= 0; i--)
                {
                    if (_candidates[i])
                    {
                        continue;
                    }

                    Debug.LogError("InteractionManager: Found null candidate, removing.");
                    _candidates.RemoveAt(i);
                }

                _candidatePositions.Clear();
                for (int i = 0; i < _candidates.Count; i++)
                {
                    _candidatePositions.Add(_candidates[i].transform.position);
                }
            }

            private bool TryGetSelectionContext(out Vector3 playerPosition, out Vector3 cameraForward, out Vector3 playerForward)
            {
                playerPosition = default;
                cameraForward = default;
                playerForward = default;

                if (_playerTransform)
                {
                    playerPosition = _playerTransform.position;
                    playerForward = _playerTransform.forward;
                }
                else if (_cam)
                {
                    playerPosition = _cam.transform.position;
                    playerForward = _cam.transform.forward;
                }
                else
                {
                    return false;
                }

                cameraForward = _cam ? _cam.transform.forward : playerForward;
                return true;
            }

            internal static int SelectBestCandidateIndex(IReadOnlyList<Vector3> candidatePositions, Vector3 playerPosition,
                Vector3 cameraForward, Vector3 playerForward, float preferredHalfAngle, float distanceWeight, float angleWeight)
            {
                if (candidatePositions == null || candidatePositions.Count == 0)
                {
                    return -1;
                }

                var selectionForward = GetSelectionForward(cameraForward, playerForward);
                var preferredCandidates = new List<CandidateMetrics>(candidatePositions.Count);
                var fallbackCandidates = new List<CandidateMetrics>(candidatePositions.Count);
                float maxPreferredSqrDistance = 0f;
                float maxFallbackSqrDistance = 0f;

                for (int i = 0; i < candidatePositions.Count; i++)
                {
                    float sqrDistance = GetPlanarSqrDistance(playerPosition, candidatePositions[i]);
                    float angleDeg = GetPlanarAngleDeg(selectionForward, playerPosition, candidatePositions[i]);
                    var metrics = new CandidateMetrics(i, sqrDistance, angleDeg);
                    fallbackCandidates.Add(metrics);
                    maxFallbackSqrDistance = Mathf.Max(maxFallbackSqrDistance, sqrDistance);

                    if (angleDeg <= preferredHalfAngle)
                    {
                        preferredCandidates.Add(metrics);
                        maxPreferredSqrDistance = Mathf.Max(maxPreferredSqrDistance, sqrDistance);
                    }
                }

                var activeCandidates = preferredCandidates.Count > 0 ? preferredCandidates : fallbackCandidates;
                float maxSubsetSqrDistance = preferredCandidates.Count > 0 ? maxPreferredSqrDistance : maxFallbackSqrDistance;
                float angleNormalizer = preferredCandidates.Count > 0 ? preferredHalfAngle : 180f;

                float bestScore = float.MaxValue;
                int bestIndex = -1;
                for (int i = activeCandidates.Count - 1; i >= 0; i--)
                {
                    var candidate = activeCandidates[i];
                    float score = CalculateCandidateScore(candidate.sqrDistance, maxSubsetSqrDistance, candidate.angleDeg,
                        angleNormalizer, distanceWeight, angleWeight);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = candidate.index;
                    }
                }

                return bestIndex;
            }

            internal static Vector3 GetSelectionForward(Vector3 cameraForward, Vector3 playerForward)
            {
                var viewForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
                if (viewForward.sqrMagnitude <= 0.0001f)
                {
                    viewForward = Vector3.ProjectOnPlane(playerForward, Vector3.up);
                }

                return viewForward.sqrMagnitude <= 0.0001f ? Vector3.forward : viewForward.normalized;
            }

            internal static float GetPlanarSqrDistance(Vector3 playerPosition, Vector3 candidatePosition)
            {
                var playerToTarget = Vector3.ProjectOnPlane(candidatePosition - playerPosition, Vector3.up);
                return playerToTarget.sqrMagnitude;
            }

            internal static float GetPlanarAngleDeg(Vector3 selectionForward, Vector3 playerPosition, Vector3 candidatePosition)
            {
                var playerToTarget = Vector3.ProjectOnPlane(candidatePosition - playerPosition, Vector3.up);
                if (playerToTarget.sqrMagnitude <= 0.0001f)
                {
                    return 0f;
                }

                return Vector3.Angle(selectionForward, playerToTarget.normalized);
            }

            internal static float CalculateCandidateScore(float sqrDistance, float maxSubsetSqrDistance, float angleDeg,
                float angleNormalizer, float distanceWeight, float angleWeight)
            {
                float normalizedDistance = maxSubsetSqrDistance <= 0.0001f ? 0f : sqrDistance / maxSubsetSqrDistance;
                float normalizedAngle = angleNormalizer <= 0.0001f ? 0f : angleDeg / angleNormalizer;
                return normalizedDistance * distanceWeight + normalizedAngle * angleWeight;
            }

            private void SetCurrent(InteractionObject next)
            {
                if (next == null)
                {
                    m_CurrentHandler?.SetTarget(null);
                    m_CurrentHandler = null;
                }
                else
                {
                    var interactableType = next.interactable.GetType();
                    
                    
                    var nextHandler = m_Type2Handlers[interactableType];

                    if (m_CurrentHandler != nextHandler)
                    {
                        m_CurrentHandler?.SetTarget(null);
                        m_CurrentHandler = nextHandler;
                    }
                
                    m_CurrentHandler.SetTarget(next);
                }
            }

            private readonly struct CandidateMetrics
            {
                public readonly int index;
                public readonly float sqrDistance;
                public readonly float angleDeg;

                public CandidateMetrics(int index, float sqrDistance, float angleDeg)
                {
                    this.index = index;
                    this.sqrDistance = sqrDistance;
                    this.angleDeg = angleDeg;
                }
            }
        }
}
