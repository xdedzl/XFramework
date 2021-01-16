namespace XFramework
{
    public static class XApplication
    {
        public static string CachePath
        {
            get
            {
                return $"{System.IO.Directory.GetCurrentDirectory()}/Library/XFramework";
            }
        }
    }
}
