using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Animation
{
    public sealed class XAnimationCueDispatcher
    {
        private sealed class PlaybackCueState
        {
            public readonly HashSet<long> TriggeredCueKeys = new();
        }

        private readonly Dictionary<string, List<XAnimationCompiledCue>> m_CuesByClipKey = new(StringComparer.Ordinal);
        private readonly Dictionary<int, PlaybackCueState> m_PlaybackStates = new();

        public event Action<XAnimationCueEvent> CueTriggered;

        public void Clear()
        {
            m_CuesByClipKey.Clear();
            m_PlaybackStates.Clear();
        }

        public void Register(IReadOnlyDictionary<string, List<XAnimationCompiledCue>> cuesByClipKey)
        {
            m_CuesByClipKey.Clear();
            m_PlaybackStates.Clear();
            if (cuesByClipKey == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<XAnimationCompiledCue>> pair in cuesByClipKey)
            {
                m_CuesByClipKey[pair.Key] = pair.Value;
            }
        }

        public void RegisterClipCues(string clipKey, IReadOnlyList<XAnimationCompiledCue> cues)
        {
            if (string.IsNullOrWhiteSpace(clipKey) || cues == null || cues.Count == 0)
            {
                return;
            }

            List<XAnimationCompiledCue> registeredCues = new(cues.Count);
            for (int i = 0; i < cues.Count; i++)
            {
                registeredCues.Add(cues[i]);
            }

            registeredCues.Sort((left, right) => left.Config.time.CompareTo(right.Config.time));
            m_CuesByClipKey[clipKey] = registeredCues;
        }

        public void ResetForPlayback(int playbackId)
        {
            m_PlaybackStates[playbackId] = new PlaybackCueState();
        }

        public void RemovePlayback(int playbackId)
        {
            m_PlaybackStates.Remove(playbackId);
        }

        public void Raise(XAnimationCueEvent cueEvent)
        {
            CueTriggered?.Invoke(cueEvent);
        }

        public void Update(
            XAnimationStatePlaybackInstance instance,
            string clipKey,
            float previousTotalNormalizedTime,
            float currentTotalNormalizedTime,
            float effectiveWeight)
        {
            if (instance == null || instance.SuppressCues)
            {
                return;
            }

            if (!m_CuesByClipKey.TryGetValue(clipKey, out List<XAnimationCompiledCue> cues) || cues.Count == 0)
            {
                return;
            }

            if (currentTotalNormalizedTime < previousTotalNormalizedTime)
            {
                return;
            }

            if (!m_PlaybackStates.TryGetValue(instance.PlaybackId, out PlaybackCueState playbackState))
            {
                playbackState = new PlaybackCueState();
                m_PlaybackStates[instance.PlaybackId] = playbackState;
            }

            int startLoop = Mathf.Max(0, Mathf.FloorToInt(previousTotalNormalizedTime));
            int endLoop = Mathf.Max(0, Mathf.FloorToInt(currentTotalNormalizedTime));
            for (int loopIndex = startLoop; loopIndex <= endLoop; loopIndex++)
            {
                float segmentStart = loopIndex == startLoop ? previousTotalNormalizedTime - startLoop : 0f;
                float segmentEnd = loopIndex == endLoop ? currentTotalNormalizedTime - endLoop : 1f;
                if (segmentEnd < segmentStart)
                {
                    segmentEnd = 1f;
                }

                for (int i = 0; i < cues.Count; i++)
                {
                    XAnimationCueConfig cueConfig = cues[i].Config;
                    bool isSegmentStartInclusive = loopIndex == startLoop && Mathf.Approximately(segmentStart, 0f);
                    bool isBeforeSegment = isSegmentStartInclusive
                        ? cueConfig.time < segmentStart
                        : cueConfig.time <= segmentStart;
                    if (isBeforeSegment || cueConfig.time > segmentEnd)
                    {
                        continue;
                    }

                    long cueKey = (((long)loopIndex) << 32) | (uint)cues[i].CueIndex;
                    if (!playbackState.TriggeredCueKeys.Add(cueKey))
                    {
                        continue;
                    }

                    CueTriggered?.Invoke(new XAnimationCueEvent
                    {
                        playbackId = instance.PlaybackId,
                        clipKey = clipKey,
                        channelName = instance.ChannelName,
                        eventKey = cueConfig.eventKey,
                        payload = cueConfig.payload,
                        weight = effectiveWeight,
                        normalizedTime = cueConfig.time,
                        loopCount = loopIndex,
                    });
                }
            }
        }
    }
}
