using System.Collections.Generic;
using UnityEngine;
using XFramework.Entity;

namespace XFramework
{
    public class AudioEntity : Entity.Entity
    {
        private AudioSource source;

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
        }

        public void Play(string path)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if(clip != null)
            {
                source.clip = clip;
                source.Play();
            }
            else
            {
                Debug.LogWarning($"sound资源不存在， path={path}");
            }
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

        private Dictionary<string, AudioClip> m_AudioClipDic;

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
        /// <param name="bgmName">背景音乐名</param>
        public void PlayBGM(string bgmName)
        {

        }


        public void PlayBGM(AudioClip audioSource)
        {

        }

        public void PlaySound(string resPath)
        {
            var auidoEntity = EntityManager.Instance.Allocate<AudioEntity>("SoundManager_Audio");
            auidoEntity.Play(resPath);
            
        }

        /// <summary>
        /// 获取音频
        /// </summary>
        public void GetAudioClip(string path)
        {

        }

        public override void Shutdown()
        {
            EntityManager.Instance.RemoveTemplate("SoundManager_Audio");
        }
    }
}