using UnityEngine;

namespace XFramework.Animation
{
    public interface IXStateMachineExitBehavior
    {
        void OnStateMachineExit(Animator animator, int stateMachinePathHash);
    }
    
    public class XStateMachineExitBehavior : StateMachineBehaviour
    {
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            var behavior = animator.GetComponent<IXStateMachineExitBehavior>();
            behavior?.OnStateMachineExit(animator, stateMachinePathHash);
        }
    }
}