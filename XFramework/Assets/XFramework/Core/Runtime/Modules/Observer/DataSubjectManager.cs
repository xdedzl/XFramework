///<summary>
///主题 有多少个basedata类型 就会有多少主题
///<summary>
namespace XFramework
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 数据主题管理类
    /// </summary>
    public class DataSubjectManager : IGameModule
    {
        /// <summary>
        /// 每一个Subject都是一个被观察的对象
        /// </summary>
        protected class Subject : ObservableSubjectTemplate<BaseData, int, object>
        {
            // 继承泛型模板定义一个非泛型主题模板
        }

        /// <summary>
        /// 存储数据类型和对应主题的字典
        /// </summary>
        private Dictionary<int, Subject> m_SubjectDic;

        public DataSubjectManager()
        {
            m_SubjectDic = new Dictionary<int, Subject>();
        }

        /// <summary>
        /// 增加数据监听
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <param name="observer">监听这个数据的观察者</param>
        public void AddListener(Enum dataType, IObserver observer)
        {
            Subject subject = null;
            int type = Convert.ToInt32(dataType);
            if (!m_SubjectDic.ContainsKey(type))
            {
                subject = new Subject();
                m_SubjectDic[type] = subject;
            }
            m_SubjectDic[type].Attach(observer.OnDataChange);
        }

        /// <summary>
        /// 删除数据监听
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <param name="observer">监听这个数据的观察者</param>
        public void RemoverListener(Enum dataType, IObserver observer)
        {
            int type = Convert.ToInt32(dataType);
            if (m_SubjectDic.ContainsKey(type))
            {
                m_SubjectDic[type].Detach(observer.OnDataChange);
            }
        }

        /// <summary>
        /// 通知事件
        /// </summary>
        /// <param name="data">data主题</param>
        /// <param name="type">事件类型</param>
        /// <param name="obj">映射参数</param>
        public void Notify(BaseData data, int type = 0, object obj = null)
        {
            if (m_SubjectDic.ContainsKey(data.dataType))
                m_SubjectDic[data.dataType].Notify(data, type, obj);
        }

        #region 接口实现

        public int Priority { get { return 3000; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

        }

        public void Shutdown()
        {

        }

        #endregion
    }
}