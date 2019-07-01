
namespace XFramework.Collections
{
    public interface IGraph<T>
    {
        void AddVertex(T data);
        void AddEdge(int fromIndex, int toIndex, int weight = 1);
    }
}