using UnityEngine;

namespace XFramework.Animation
{
    public interface IXAnimationStateEnterBehavior
    {
        void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex);
    }
    
    public class XStateEnterBehavior : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var behavior = animator.GetComponent<IXAnimationStateEnterBehavior>();
            behavior?.OnStateEnter(animator, stateInfo, layerIndex);
        }
    }
}