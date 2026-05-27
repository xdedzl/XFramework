using UnityEngine.Playables;

namespace XFramework.Animation
{
    internal sealed class XAnimationCuePlayableBehaviour : PlayableBehaviour
    {
        private XAnimationDriver m_Driver;

        public void Bind(XAnimationDriver driver)
        {
            m_Driver = driver;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            m_Driver?.CollectCuesFromPlayableGraph();
        }
    }
}
