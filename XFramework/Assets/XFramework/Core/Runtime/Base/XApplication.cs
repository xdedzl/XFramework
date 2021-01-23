using System.IO;

namespace XFramework
{
    public static class XApplication
    {
        public static string CachePath
        {
            get
            {
                string path = $"{Directory.GetCurrentDirectory()}/Library/XFramework";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }
    }
}
