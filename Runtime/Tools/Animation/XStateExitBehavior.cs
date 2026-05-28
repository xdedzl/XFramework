using UnityEngine;

namespace XAnimationEngine
{
    public interface IXAnimationStateExitBehavior
    {
        void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex);
    }
    
    public class XStateExitBehavior : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var behavior = animator.GetComponent<IXAnimationStateExitBehavior>();
            behavior?.OnStateExit(animator, stateInfo, layerIndex);
        }
    }
}