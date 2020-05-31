﻿using System.Runtime.CompilerServices;
using XFramework;

namespace XFramework
{
    public abstract class GameModuleBase<T> : IGameModule where T : GameModuleBase<T>
    {
        /// <summary>
        /// 模块优先级
        /// </summary>
        /// <remarks>优先级较高的模块会优先轮询</remarks>
        public abstract int Priority { get; }

        // 该变量名称和GameEntry有联系, 不要随意修改
        private static T m_instance;

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
        /// <summary>
        /// 关闭模块
        /// </summary> 
        public virtual void Shutdown() { }
        /// <summary>
        /// 游戏框架模块轮询
        /// </summary>
        /// <param name="elapseSeconds">逻辑运行时间，以秒为单位</param>
        /// <param name="realElapseSeconds">真实运行时间，以秒为单位</param>
        public virtual void Update(float elapseSeconds, float realElapseSeconds) { }
    }
}