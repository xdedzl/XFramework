using UnityEngine;

namespace XFramework.Interaction
{
    public class MusicPlayer : MonoBehaviour, IKeyCodeInteractable
    {
        [SerializeField]
        [AssetPath(typeof(AudioClip))]
        private string bgmPath;

        private bool m_IsPlayingManualBgm;
        
        public void Interact()
        {
            if (!m_IsPlayingManualBgm)
            {
                SoundManager.Instance.PlayBgm(bgmPath);
                m_IsPlayingManualBgm = true;
            }
            else
            {
                SoundManager.Instance.ClearManualBgm();
                m_IsPlayingManualBgm = false;
            }
        }
    }
}
