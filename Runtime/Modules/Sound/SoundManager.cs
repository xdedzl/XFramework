using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using XFramework.Entity;
using XFramework.Resource;
using XFramework.Tasks;

namespace XFramework
{
    public interface IAudioPlayback
    {
        bool IsValid { get; }
        void Stop();
    }

    public readonly struct SoundManagerDebugSnapshot
    {
        public SoundManagerDebugSnapshot(
            bool isPlayingBgm,
            bool bgmPaused,
            bool manualBgmOverride,
            float bgmVolume,
            string currentBgmClipName,
            AreaBgmVolume currentBgmVolume,
            IReadOnlyList<AreaBgmVolume> activeBgmVolumes,
            int cachedAudioClipCount)
        {
            IsPlayingBgm = isPlayingBgm;
            BgmPaused = bgmPaused;
            ManualBgmOverride = manualBgmOverride;
            BgmVolume = bgmVolume;
            CurrentBgmClipName = currentBgmClipName;
            CurrentBgmVolume = currentBgmVolume;
            ActiveBgmVolumes = activeBgmVolumes;
            CachedAudioClipCount = cachedAudioClipCount;
        }

        public bool IsPlayingBgm { get; }
        public bool BgmPaused { get; }
        public bool ManualBgmOverride { get; }
        public float BgmVolume { get; }
        public string CurrentBgmClipName { get; }
        public AreaBgmVolume CurrentBgmVolume { get; }
        public IReadOnlyList<AreaBgmVolume> ActiveBgmVolumes { get; }
        public int CachedAudioClipCount { get; }
    }

    [DependenceModule(typeof(EntityManager))]
    [DependenceModule(typeof(ResourceManager))]
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public class SoundManager : GameModuleBase<SoundManager>
    {
        private const float DefaultBgmFadeDuration = 0.8f;

        private sealed class AudioEntity : Entity.Entity, IAudioPlayback
        {
            private AudioSource source;
            private ITask xTask;
        
            public bool isPlaying => source.isPlaying;

            public override void OnInit()
            {
                source = GetComponent<AudioSource>();
            }

            public override void OnAllocate(IEntityData entityData)
            {
                gameObject.SetActive(true);
            }

            public override void OnRecycle()
            {
                gameObject.SetActive(false);
                xTask?.Stop();
                xTask = null;
                source.Stop();
                source.clip = null;
                source.pitch = 1f;
                source.spatialBlend = 0f;
            }

            public void Stop()
            {
                if (IsValid)
                {
                    Recycle();
                }
            }

            public ITask Play(AudioClip clip, float volume = 1f, float pitch = 1f)
            {
                xTask?.Stop();
                xTask = null;
            
                source.volume = volume;
                source.clip = clip;
                source.pitch = pitch;
                source.spatialBlend = 0f;
                source.Play();
                xTask = XTask.Delay(ResolveClipDuration(clip, pitch));
                var next = xTask.ContinueWith(() =>
                {
                    xTask = null;
                    Recycle();
                });
                return next;
            }
        
            public ITask Play3D(AudioClip clip, Vector3 position, float minDistance, float maxDistance, float volume = 1f, float pitch = 1f)
            {
                xTask?.Stop();
                xTask = null;
            
                transform.position = position;
                source.clip = clip;
                source.volume = volume;
                source.pitch = pitch;
                source.spatialBlend = 1f; // 3D 声音
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.Play();
                xTask = XTask.Delay(ResolveClipDuration(clip, pitch));
                var next = xTask.ContinueWith(() =>
                {
                    xTask = null;
                    Recycle();
                });
                return next;
            }

            private static float ResolveClipDuration(AudioClip clip, float pitch)
            {
                return clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            }
        }

        /// <summary>
        /// 背景音乐
        /// </summary>
        private AudioSource m_BGM;
        private AudioSource m_BGMFade;
        private AudioSource m_ActiveBGM;
        private Coroutine m_BgmFadeCoroutine;
        /// <summary>
        /// 2D音效
        /// </summary>
        private List<AudioSource> m_SFX2D;
        /// <summary>
        /// 3D音效
        /// </summary>
        private List<AudioSource> m_SFX3D;

        private readonly Dictionary<string, AudioClip> m_AudioClipDic = new();
        private readonly List<AreaBgmVolume> m_ActiveBgmVolumes = new();
        private AreaBgmVolume m_CurrentBgmVolume;
        private bool m_ManualBgmOverride;
        private bool m_BgmPaused;
        private float m_BgmVolume = 1f;

        public SoundManager()
        {
            var res = new GameObject("audio-templete");
            res.AddComponent<AudioSource>();
            EntityManager.Instance.AddTemplate<AudioEntity>("SoundManager_Audio", res);
        }

        /// <summary>
        /// 播放背景音乐
        /// </summary>
        public void PlayBgm(string path)
        {
            PlayBgm(GetAudioClip(path));
        }

        public void PlayBgm(AudioClip clip)
        {
            m_BgmPaused = false;
            m_ManualBgmOverride = true;
            PlayBgmInternal(clip);
        }

        public void ClearManualBgm()
        {
            m_ManualBgmOverride = false;
            RefreshAreaBgm(true);
        }

        /// <summary>
        /// 暂停并停止所有 BGM，后续区域 BGM 只更新状态，不会自动播放。
        /// </summary>
        public void StopBgm()
        {
            m_BgmPaused = true;
            m_ManualBgmOverride = false;
            StopBgmInternal(DefaultBgmFadeDuration);
        }

        /// <summary>
        /// 恢复 BGM 播放开关，并播放当前应生效的区域 BGM。
        /// </summary>
        public void ResumeBgm()
        {
            m_BgmPaused = false;
            RefreshAreaBgm(true);
        }

        /// <summary>
        /// BGM 是否处于全局暂停状态。
        /// </summary>
        public bool IsBgmPaused()
        {
            return m_BgmPaused;
        }

        public bool IsPlayBgm()
        {
            return (m_BGM != null && m_BGM.isPlaying) || (m_BGMFade != null && m_BGMFade.isPlaying);
        }

        public float GetBgmVolume()
        {
            return m_BgmVolume;
        }

        public void SetBgmVolume(float volume)
        {
            m_BgmVolume = Mathf.Clamp01(volume);
            ApplyBgmVolume();
        }

        public SoundManagerDebugSnapshot GetDebugSnapshot()
        {
            List<AreaBgmVolume> activeVolumes = new();
            for (int i = 0; i < m_ActiveBgmVolumes.Count; i++)
            {
                AreaBgmVolume volume = m_ActiveBgmVolumes[i];
                if (volume != null)
                {
                    activeVolumes.Add(volume);
                }
            }

            return new SoundManagerDebugSnapshot(
                IsPlayBgm(),
                m_BgmPaused,
                m_ManualBgmOverride,
                m_BgmVolume,
                m_ActiveBGM != null && m_ActiveBGM.clip != null ? m_ActiveBGM.clip.name : string.Empty,
                m_CurrentBgmVolume,
                activeVolumes,
                m_AudioClipDic.Count);
        }

        public void PlayWebBgm(string webPath)
        {
            MonoEvent.Instance.StartCoroutine(LoadAndPlayAudio(webPath));
        }

        public void EnterBgmVolume(AreaBgmVolume volume)
        {
            if (volume == null || !volume.HasBgmClip)
            {
                return;
            }

            m_ActiveBgmVolumes.Remove(volume);
            m_ActiveBgmVolumes.Add(volume);
            RefreshAreaBgm(false);
        }

        public void ExitBgmVolume(AreaBgmVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            m_ActiveBgmVolumes.Remove(volume);
            RefreshAreaBgm(false);
        }

        public void RefreshBgmVolumes()
        {
            RefreshAreaBgm(true);
        }

        public void RefreshBgmVolume(AreaBgmVolume volume)
        {
            if (volume == null)
            {
                RefreshAreaBgm(true);
                return;
            }

            bool isActiveVolume = m_ActiveBgmVolumes.Contains(volume);
            AreaBgmVolumeDebugSnapshot snapshot = volume.GetDebugSnapshot();
            if (!isActiveVolume && snapshot.PlayerColliderCount > 0 && volume.HasBgmClip)
            {
                m_ActiveBgmVolumes.Add(volume);
            }

            RefreshAreaBgm(true);
        }

        private IEnumerator LoadAndPlayAudio(string url)
        {
            // 使用UnityWebRequest加载音频，指定AudioType为MPEG（MP3）或OGGVORBIS（OGG）
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 获取音频剪辑并播放
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                PlayBgm(clip);
            }
            else
            {
                Debug.LogError($"加载音频失败: {request.error}");
            }
        }

        public IAudioPlayback PlaySound(string path, float volume = 1f, float pitch = 1f)
        {
            var clip = GetAudioClip(path);
            return PlaySound(clip, volume, pitch);
        }
        
        public IAudioPlayback PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if(!clip)
            {
                Debug.LogWarning($"sound资源不存在");
                return null;
            }
            else
            {
                var audioEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
                audioEntity.Play(clip, volume, pitch);
                LogPlaySound(clip, volume, pitch);
                return audioEntity;
            }
        }
        
        public IAudioPlayback PlaySound3D(string path, Vector3 position, float minDistance = 1f, float maxDistance=20f, float volume = 1f, float pitch = 1f)
        {
            var clip = GetAudioClip(path);
            return PlaySound3D(clip, position, minDistance, maxDistance, volume, pitch);
        }
        
        public IAudioPlayback PlaySound3D(AudioClip clip, Vector3 position, float minDistance = 1f, float maxDistance=20f, float volume = 1f, float pitch = 1f)
        {
            if(!clip)
            {
                Debug.LogWarning($"sound资源不存在");
                return null;
            }
            else
            {
                var audioEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
                audioEntity.Play3D(clip, position, minDistance, maxDistance, volume, pitch);
                LogPlaySound(clip, volume, pitch);
                return audioEntity;
            }
        }

        /// <summary>
        /// 获取音频
        /// </summary>
        public AudioClip GetAudioClip(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("SoundManager找不到音频资源:路径为空");
                return null;
            }

            if (!m_AudioClipDic.ContainsKey(path))
            {
                AudioClip clip = ResourceManager.Instance.Load<AudioClip>(path);
                if(clip==null)
                {
                    Debug.LogWarning($"SoundManager找不到音频资源:{path}");
                    return null;
                }
                m_AudioClipDic[path] = clip;
            }

            return m_AudioClipDic[path];
        }

        public override void Shutdown()
        {
            m_BgmPaused = false;
            m_ManualBgmOverride = false;
            StopBgmInternal(DefaultBgmFadeDuration);
            m_ActiveBgmVolumes.Clear();
            EntityManager.Instance.RemoveTemplate("SoundManager_Audio");
        }

        private void RefreshAreaBgm(bool forceReplay)
        {
            AreaBgmVolume nextVolume = ResolveCurrentBgmVolume();
            AreaBgmVolume previousVolume = m_CurrentBgmVolume;
            m_CurrentBgmVolume = nextVolume;

            if (m_BgmPaused || m_ManualBgmOverride)
            {
                return;
            }

            if (!forceReplay && previousVolume == nextVolume)
            {
                return;
            }

            if (nextVolume == null)
            {
                StopBgmInternal(DefaultBgmFadeDuration);
                return;
            }

            string bgmPath = nextVolume.GetBgmPath();
            PlayBgmInternal(string.IsNullOrWhiteSpace(bgmPath) ? null : GetAudioClip(bgmPath));
        }

        private AreaBgmVolume ResolveCurrentBgmVolume()
        {
            AreaBgmVolume bestVolume = null;
            int bestPriority = int.MinValue;

            for (int i = m_ActiveBgmVolumes.Count - 1; i >= 0; i--)
            {
                AreaBgmVolume volume = m_ActiveBgmVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasBgmClip)
                {
                    m_ActiveBgmVolumes.RemoveAt(i);
                    continue;
                }

                if (bestVolume == null || volume.Priority > bestPriority)
                {
                    bestVolume = volume;
                    bestPriority = volume.Priority;
                }
            }

            return bestVolume;
        }

        private void PlayBgmInternal(AudioClip clip)
        {
            if (!clip)
            {
                StopBgmInternal(DefaultBgmFadeDuration);
                return;
            }

            if (m_BGM == null)
            {
                CreateBgmSources();
            }

            if (m_ActiveBGM != null && m_ActiveBGM.clip == clip && m_ActiveBGM.isPlaying)
            {
                return;
            }

            AudioSource nextSource = m_ActiveBGM == m_BGM ? m_BGMFade : m_BGM;
            AudioSource previousSource = m_ActiveBGM;
            StartBgmFade(previousSource, nextSource, clip, DefaultBgmFadeDuration);
        }

        private void StopBgmInternal(float fadeDuration)
        {
            if (m_BgmFadeCoroutine != null)
            {
                MonoEvent.Instance.StopCoroutine(m_BgmFadeCoroutine);
                m_BgmFadeCoroutine = null;
            }

            m_ActiveBGM = null;

            bool hasPlayingSource = (m_BGM != null && m_BGM.isPlaying) || (m_BGMFade != null && m_BGMFade.isPlaying);
            if (!hasPlayingSource || fadeDuration <= 0f)
            {
                StopBgmSource(m_BGM);
                StopBgmSource(m_BGMFade);
                return;
            }

            m_BgmFadeCoroutine = MonoEvent.Instance.StartCoroutine(FadeOutBgmSources(m_BGM, m_BGMFade, fadeDuration));
        }

        private void StartBgmFade(AudioSource previousSource, AudioSource nextSource, AudioClip clip, float fadeDuration)
        {
            if (m_BgmFadeCoroutine != null)
            {
                MonoEvent.Instance.StopCoroutine(m_BgmFadeCoroutine);
                m_BgmFadeCoroutine = null;
            }

            nextSource.clip = clip;
            nextSource.loop = true;
            nextSource.volume = fadeDuration <= 0f ? m_BgmVolume : 0f;
            nextSource.Play();
            m_ActiveBGM = nextSource;

            if (fadeDuration <= 0f)
            {
                nextSource.volume = m_BgmVolume;
                StopBgmSource(previousSource);
                return;
            }

            m_BgmFadeCoroutine = MonoEvent.Instance.StartCoroutine(FadeBgm(previousSource, nextSource, fadeDuration));
        }

        private IEnumerator FadeBgm(AudioSource previousSource, AudioSource nextSource, float fadeDuration)
        {
            float elapsed = 0f;
            float previousStartVolume = previousSource != null ? previousSource.volume : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);

                if (previousSource != null)
                {
                    previousSource.volume = Mathf.Lerp(previousStartVolume, 0f, progress);
                }

                if (nextSource != null)
                {
                    nextSource.volume = Mathf.Lerp(0f, m_BgmVolume, progress);
                }

                yield return null;
            }

            if (nextSource != null)
            {
                nextSource.volume = m_BgmVolume;
            }

            StopBgmSource(previousSource);
            m_BgmFadeCoroutine = null;
        }

        private IEnumerator FadeOutBgmSources(AudioSource firstSource, AudioSource secondSource, float fadeDuration)
        {
            float elapsed = 0f;
            float firstStartVolume = firstSource != null ? firstSource.volume : 0f;
            float secondStartVolume = secondSource != null ? secondSource.volume : 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                if (firstSource != null)
                {
                    firstSource.volume = Mathf.Lerp(firstStartVolume, 0f, progress);
                }

                if (secondSource != null)
                {
                    secondSource.volume = Mathf.Lerp(secondStartVolume, 0f, progress);
                }

                yield return null;
            }

            StopBgmSource(firstSource);
            StopBgmSource(secondSource);
            m_BgmFadeCoroutine = null;
        }

        private void CreateBgmSources()
        {
            var res = new GameObject("audio-bgm");
            m_BGM = res.AddComponent<AudioSource>();
            m_BGM.loop = true;
            m_BGM.volume = 0f;

            m_BGMFade = res.AddComponent<AudioSource>();
            m_BGMFade.loop = true;
            m_BGMFade.volume = 0f;
        }

        private static void StopBgmSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        private void StopInactiveBgmSources(AudioSource activeSource)
        {
            if (m_BGM != activeSource)
            {
                StopBgmSource(m_BGM);
            }

            if (m_BGMFade != activeSource)
            {
                StopBgmSource(m_BGMFade);
            }
        }

        private void ApplyBgmVolume()
        {
            if (m_ActiveBGM != null && m_ActiveBGM.isPlaying)
            {
                m_ActiveBGM.volume = m_BgmVolume;
            }
        }

        private static void LogPlaySound(AudioClip clip, float volume, float pitch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SoundManager] Play sound '{clip.name}'. volume={volume}, pitch={pitch}");
#endif
        }
    }
}
