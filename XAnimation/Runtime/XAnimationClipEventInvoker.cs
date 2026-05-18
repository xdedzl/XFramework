using System;
using UnityEngine;

namespace XFramework.Animation
{
    internal static class XAnimationClipEventInvoker
    {
        private const float DispatchWeightThreshold = 0.5f;

        public static void Dispatch(
            AnimationClip clip,
            XAnimationStatePlaybackInstance instance,
            float previousTotalNormalizedTime,
            float currentTotalNormalizedTime,
            float effectiveWeight,
            Action<XAnimationCueEvent> onCueTriggered)
        {
            if (clip == null ||
                instance == null ||
                onCueTriggered == null ||
                effectiveWeight <= DispatchWeightThreshold ||
                currentTotalNormalizedTime < previousTotalNormalizedTime)
            {
                return;
            }

            AnimationEvent[] events = clip.events;
            if (events == null || events.Length == 0)
            {
                return;
            }

            float clipLength = Mathf.Max(clip.length, 0.0001f);
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

                for (int i = 0; i < events.Length; i++)
                {
                    AnimationEvent animationEvent = events[i];
                    if (animationEvent == null || string.IsNullOrWhiteSpace(animationEvent.functionName))
                    {
                        continue;
                    }

                    float normalizedTime = Mathf.Clamp01(animationEvent.time / clipLength);
                    bool isSegmentStartInclusive = loopIndex == startLoop && Mathf.Approximately(segmentStart, 0f);
                    bool isBeforeSegment = isSegmentStartInclusive
                        ? normalizedTime < segmentStart
                        : normalizedTime <= segmentStart;
                    if (isBeforeSegment || normalizedTime > segmentEnd)
                    {
                        continue;
                    }

                    onCueTriggered(BuildCueEvent(instance, clip, animationEvent, normalizedTime, loopIndex, effectiveWeight));
                }
            }
        }

        private static XAnimationCueEvent BuildCueEvent(
            XAnimationStatePlaybackInstance instance,
            AnimationClip clip,
            AnimationEvent animationEvent,
            float normalizedTime,
            int loopCount,
            float effectiveWeight)
        {
            return new XAnimationCueEvent
            {
                playbackId = instance.PlaybackId,
                clipKey = instance.PrimaryClipKey,
                channelName = instance.ChannelName,
                eventKey = animationEvent.functionName,
                payload = ResolvePayload(animationEvent),
                weight = effectiveWeight,
                normalizedTime = normalizedTime,
                loopCount = loopCount,
            };
        }

        private static string ResolvePayload(AnimationEvent animationEvent)
        {
            if (animationEvent == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(animationEvent.stringParameter))
            {
                return animationEvent.stringParameter;
            }

            if (animationEvent.intParameter != 0)
            {
                return animationEvent.intParameter.ToString();
            }

            if (!Mathf.Approximately(animationEvent.floatParameter, 0f))
            {
                return animationEvent.floatParameter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (animationEvent.objectReferenceParameter != null)
            {
                return animationEvent.objectReferenceParameter.name ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
