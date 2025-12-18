namespace XFramework
{
    /// <summary>
    /// 定义显示在ui或其他编辑器菜单中的路径
    /// </summary>
    public class MenuPathAttribute : System.Attribute
    {
        public string path;
        public MenuPathAttribute(string path)
        {
            this.path = path;
        }
    }
}