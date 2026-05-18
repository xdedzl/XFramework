#if UNITY_EDITOR
using UnityEditor;

namespace XFramework.Editor
{
    internal sealed class XAnimationPlaybackSettings
    {
        public bool PlaybackSectionExpanded = true;
        public bool TransitionSectionExpanded;
        public string ChannelName = string.Empty;
        public float Speed = 1f;
        public bool ApplyTransition;
        public float FadeIn;
        public float FadeOut;
        public float EnterTime;
        public int Priority;
        public bool Interruptible = true;
    }

    internal static class XAnimationPlaybackSettingsPrefs
    {
        private const string Prefix = "XFramework.Editor.XAnimation.PlaybackSettings.";

        public static XAnimationPlaybackSettings Load()
        {
            return new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackSectionExpanded), true),
                TransitionSectionExpanded = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.TransitionSectionExpanded), false),
                ChannelName = EditorPrefs.GetString(Prefix + nameof(XAnimationPlaybackSettings.ChannelName), string.Empty),
                Speed = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.Speed), 1f),
                ApplyTransition = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyTransition), false),
                FadeIn = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeIn), 0f),
                FadeOut = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeOut), 0f),
                EnterTime = EditorPrefs.GetFloat(Prefix + nameof(XAnimationPlaybackSettings.EnterTime), 0f),
                Priority = EditorPrefs.GetInt(Prefix + nameof(XAnimationPlaybackSettings.Priority), 0),
                Interruptible = EditorPrefs.GetBool(Prefix + nameof(XAnimationPlaybackSettings.Interruptible), true),
            };
        }

        public static void Save(XAnimationPlaybackSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.PlaybackSectionExpanded), settings.PlaybackSectionExpanded);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.TransitionSectionExpanded), settings.TransitionSectionExpanded);
            EditorPrefs.SetString(Prefix + nameof(XAnimationPlaybackSettings.ChannelName), settings.ChannelName ?? string.Empty);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.Speed), settings.Speed);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.ApplyTransition), settings.ApplyTransition);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeIn), settings.FadeIn);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.FadeOut), settings.FadeOut);
            EditorPrefs.SetFloat(Prefix + nameof(XAnimationPlaybackSettings.EnterTime), settings.EnterTime);
            EditorPrefs.SetInt(Prefix + nameof(XAnimationPlaybackSettings.Priority), settings.Priority);
            EditorPrefs.SetBool(Prefix + nameof(XAnimationPlaybackSettings.Interruptible), settings.Interruptible);
        }
    }
}
#endif
