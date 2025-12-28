using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using XFramework.Entity;
using XFramework.Resource;
using XFramework.Tasks;

namespace XFramework
{
    public class AudioEntity : Entity.Entity
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
        }

        public ITask Play(AudioClip clip, float volume = 1f)
        {
            xTask?.Stop();
            xTask = null;
            
            source.volume = volume;
            source.clip = clip;
            source.Play();
            xTask = XTask.Delay(clip.length);
            var next = xTask.ContinueWith(() =>
            {
                xTask = null;
                Recycle();
            });
            return next;
        }
        
        public ITask Play3D(AudioClip clip, Vector3 position, float minDistance, float maxDistance, float volume = 1f)
        {
            xTask?.Stop();
            xTask = null;
            
            transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f; // 3D 声音
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            xTask = XTask.Delay(clip.length);
            var next = xTask.ContinueWith(() =>
            {
                xTask = null;
                Recycle();
            });
            return next;
        }
    }

    [DependenceModule(typeof(EntityManager))]
    public class SoundManager : PersistentGameModuleBase<SoundManager>
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
        
        public AudioEntity PlaySound3D(string path, Vector3 position, float minDistance = 1f, float maxDistance=20f, float volume = 1f)
        {
            var clip = GetAudioClip(path);
            return PlaySound3D(clip, position, minDistance, maxDistance, volume);
        }
        
        public AudioEntity PlaySound3D(AudioClip clip, Vector3 position, float minDistance = 1f, float maxDistance=20f, float volume = 1f)
        {
            if(!clip)
            {
                Debug.LogWarning($"sound资源不存在");
                return null;
            }
            else
            {
                var audioEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
                audioEntity.Play3D(clip, position, minDistance, maxDistance, volume);
                return audioEntity;
            }
        }

        /// <summary>
        /// 获取音频
        /// </summary>
        public AudioClip GetAudioClip(string path)
        {
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
            EntityManager.Instance.RemoveTemplate("SoundManager_Audio");
        }
    }
}