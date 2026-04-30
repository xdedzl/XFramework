#if UNITY_EDITOR
using UnityEditor;

namespace XFramework.Editor
{
    internal sealed class XAnimationPlaybackSettings
    {
        public bool PlaybackSectionExpanded = true;
        public bool TargetSectionExpanded = true;
        public bool TransitionSectionExpanded;
        public bool PlaybackOptionsSectionExpanded;
        public string ChannelName = string.Empty;
        public float Speed = 1f;
        public bool ApplyTransition;
        public float FadeIn;
        public float FadeOut;
        public int Priority;
        public bool Interruptible = true;
        public bool ApplyPlayback;
        public float Weight = 1f;
        public float NormalizedTime;
        public bool UseLoopOverride;
        public bool LoopOverride = true;
        public bool UseRootMotionOverride;
        public bool RootMotionOverride;
    }

    internal static class XAnimationPlaybackSettingsPrefs
    {
        private const string Prefix = "XFramework.Editor.XAnimation.PlaybackSettings.";

        public static XAnimationPlaybackSettings Load()
        {
            return new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackSectionExpanded), true),
                TargetSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.TargetSectionExpanded), true),
                TransitionSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.TransitionSectionExpanded), false),
                PlaybackOptionsSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackOptionsSectionExpanded), false),
                ChannelName = EditorPrefs.GetString(Prefix + nameof(XAnimationPlaybackSettings.ChannelName), string.Empty),
                Speed = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.Speed), 1f),
                ApplyTransition = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyTransition), false),
                FadeIn = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeIn), 0f),
                FadeOut = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeOut), 0f),
                Priority = EditorPrefs.GetInt(Prefix + nameof(XAnimationPlaybackSettings.Priority), 0),
                Interruptible = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.Interruptible), true),
                ApplyPlayback = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyPlayback), false),
                Weight = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.Weight), 1f),
                NormalizedTime = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.NormalizedTime), 0f),
                UseLoopOverride = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.UseLoopOverride), false),
                LoopOverride = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.LoopOverride), true),
                UseRootMotionOverride = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.UseRootMotionOverride), false),
                RootMotionOverride = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.RootMotionOverride), false),
            };
        }

        public static void Save(XAnimationPlaybackSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackSectionExpanded), settings.PlaybackSectionExpanded);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.TargetSectionExpanded), settings.TargetSectionExpanded);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.TransitionSectionExpanded), settings.TransitionSectionExpanded);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackOptionsSectionExpanded), settings.PlaybackOptionsSectionExpanded);
            EditorPrefs.SetString(Prefix + nameof(XAnimationPlaybackSettings.ChannelName), settings.ChannelName ?? string.Empty);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.Speed), settings.Speed);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyTransition), settings.ApplyTransition);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeIn), settings.FadeIn);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeOut), settings.FadeOut);
            EditorPrefs.SetInt(Prefix + nameof(XAnimationPlaybackSettings.Priority), settings.Priority);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.Interruptible), settings.Interruptible);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyPlayback), settings.ApplyPlayback);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.Weight), settings.Weight);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.NormalizedTime), settings.NormalizedTime);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.UseLoopOverride), settings.UseLoopOverride);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.LoopOverride), settings.LoopOverride);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.UseRootMotionOverride), settings.UseRootMotionOverride);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.RootMotionOverride), settings.RootMotionOverride);
        }
    }
}
#endif
