using System.Runtime.CompilerServices;
using XFramework;

namespace XFramework
{
    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        public abstract int Priority { get; }

        public static T m_instance;

        public static T Instance
        {
            get
            {
                if (m_instance != null)
                {
                    return m_instance;
                }

                throw new FrameworkException($"使用{typeof(T).Name}前需先加载模块");
            }
        }

        public virtual void Shutdown() { }

        public virtual void Update(float elapseSeconds, float realElapseSeconds) { }

        public static void CreateSelf(params object[] args)
        {
            m_instance = GameEntry.AddModule<T>(args);
        }

        public static void ShutdownSelf()
        {
            GameEntry.ShutdownModule<T>();
            m_instance = null;
        }
    }
}