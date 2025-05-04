using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public class SoundManager : Singleton<SoundManager>
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

        public int Priority { get { return 100; } }

        public void Shutdown()
        {

        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

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

        /// <summary>
        /// 获取音频
        /// </summary>
        public void GetAudioClip(string path)
        {

        }
    }
}