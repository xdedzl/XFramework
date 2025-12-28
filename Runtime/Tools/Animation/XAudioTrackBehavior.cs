using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using XFramework.Resource;

namespace XFramework.Animation
{
    [Serializable]
    public struct XAudioTrackKeyframe
    {
        public float time;
        [AssetPath]
        public string clip;
    }
    
    public class XAudioTrackBehavior: StateMachineBehaviour
    {
        public XAudioTrackKeyframe[] keyframes;
        
        private Queue<XAudioTrackKeyframe> playedKeyframes = new Queue<XAudioTrackKeyframe>();

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            base.OnStateUpdate(animator, stateInfo, layerIndex);
            float normalizedTime = stateInfo.normalizedTime % 1f;
            Debug.Log(normalizedTime);
            if (Mathf.Approximately(normalizedTime, 0f))
            {
                ResetAudios();
            }

            while (playedKeyframes.Count > 0)
            {
                var nextKeyframe = playedKeyframes.Peek();
                if (normalizedTime >= nextKeyframe.time)
                {
                    nextKeyframe = playedKeyframes.Dequeue();
                    SoundManager.Instance.PlaySound(nextKeyframe.clip);
                }
            }
        }

        private void ResetAudios()
        {
            playedKeyframes.Clear();

            var list = keyframes.ToList();
            list.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (var keyframe in list)
            {
                playedKeyframes.Enqueue(keyframe);
            }
        }
    }
}