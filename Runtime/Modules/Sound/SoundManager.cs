using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using XFramework.Entity;

namespace XFramework
{
    public class AudioEntity : Entity.Entity
    {
        private AudioSource source;
        private Timer timer;

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
            timer?.Stop();
            timer = null;
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            timer?.Stop();
            timer = null;
            
            source.volume = volume;
            source.clip = clip;
            source.Play();
            timer = Timer.Register(clip.length, () =>
            {
                timer = null;
                Recycle();
            });
        }
    }

    [DependenceModule(typeof(EntityManager))]
    public class SoundManager : GameModuleBase<SoundManager>
    {
        /// <summary>
        /// 背景音乐
        /// </summary>
        private AudioSource m_BGM;
        /// <summary>
        /// 2D音效
        /// </summary>
        private List<AudioSource> m_SFX2D;
        /// <summary>
        /// 3D音效
        /// </summary>
        private List<AudioSource> m_SFX3D;

        private readonly Dictionary<string, AudioClip> m_AudioClipDic = new();

        public override int Priority => 9999;

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
            AudioClip clip = Resources.Load<AudioClip>(path);
            PlayBgm(clip);
        }

        public void PlayBgm(AudioClip clip)
        {
            if (m_BGM == null)
            {
                var res = new GameObject("audio-bgm");
                m_BGM = res.AddComponent<AudioSource>();
                m_BGM.loop = true;
            }
            m_BGM.clip = clip;
            m_BGM.Play();
        }

        public void StopBgm()
        {
            m_BGM?.Stop();
        }

        public void PlayWebBgm(string webPath)
        {
            MonoEvent.Instance.StartCoroutine(LoadAndPlayAudio(webPath));
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

        public void PlaySound(string path, float volume = 1f)
        {
            var clip = GetAudioClip(path);
            PlaySound(clip, volume);
        }
        
        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if(!clip)
            {
                Debug.LogWarning($"sound资源不存在");
            }
            else
            {
                var audioEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
                audioEntity.Play(clip, volume);
            }
        }
        
        public void PlaySound3D(string path, Vector3 position, float volume = 1f)
        {
            var clip = GetAudioClip(path);
            PlaySound3D(clip, position, volume);
        }
        
        public void PlaySound3D(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if(!clip)
            {
                Debug.LogWarning($"sound资源不存在");
            }
            else
            {
                var audioEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
                audioEntity.transform.position = position;
                audioEntity.Play(clip, volume);
            }
        }

        /// <summary>
        /// 获取音频
        /// </summary>
        public AudioClip GetAudioClip(string path)
        {
            if (!m_AudioClipDic.ContainsKey(path))
            {
                AudioClip clip = Resources.Load<AudioClip>(path);
                m_AudioClipDic[path] = clip;
            }

            return m_AudioClipDic[path];
        }

        public override void Shutdown()
        {
            EntityManager.Instance.RemoveTemplate("SoundManager_Audio");
        }
    }
}