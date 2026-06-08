using System.Collections.Generic;
using UnityEngine;

namespace XFramework.NodeKit
{
    public interface IXNode
    {
        public string GetId();
        public string name { get; set; }
    }
    
    public abstract class XNodeBase : IXNode
    {
        [HideInInspector]
        public string id = System.Guid.NewGuid().ToString();
        [HideInInspector]
        public string name { get; set; }
        
        public string GetId()
        {
            return id;
        }
    }
    
    
    #region 过程节点类型
    /// <summary>
    /// 过程节点, 进行一些操作后可以流转到后续节点的节点
    /// </summary>
    public interface IProcessNode : IXNode
    {
        public IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args);
        public void OnNodeStart(IXNodeGraph storyGraph) {}
        
        public void OnNodeEnd(IXNodeGraph storyGraph) {}
    }
    
    /// <summary>
    /// 选择器节点
    /// </summary>
    public interface ISwitchNode : IProcessNode
    {
        
    }

    /// <summary>
    /// 剧情节点
    /// </summary>
    public interface IPlotNode: IProcessNode
    {
        
    }
    #endregion
    
    #region 数值节点类型
    /// <summary>
    /// 数值节点，用于向其他节点传递数据
    /// </summary>
    public interface IValueNode : IXNode
    {
        
    }
    
    
    /// <summary>
    /// 泛型数值节点，用于向其他节点传递数据
    /// </summary>
    public interface IValueNode<out T>: IValueNode
    {
        public T GetValue();
    }
    #endregion
}
