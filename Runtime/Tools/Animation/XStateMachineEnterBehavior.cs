using UnityEngine;

namespace XFramework.Animation
{
    public interface IXStateMachineEnterBehavior
    {
        void OnStateMachineEnter(Animator animator, int stateMachinePathHash);
    }
    
    public class XStateMachineEnterBehavior : StateMachineBehaviour
    {
        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
        {
            var behavior = animator.GetComponent<IXStateMachineEnterBehavior>();
            behavior?.OnStateMachineEnter(animator, stateMachinePathHash);
        }
    }
}