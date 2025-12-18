using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;

namespace XFramework
{
    /// <summary>
    /// 实用工具方法类
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// IO相关工具方法类
        /// </summary>
        public static class IO
        {
            public enum CreatType
            {
                CreateNew,
                KeepOld,
            }
            
            #region 序列化相关

            ///<summary> 
            /// 序列化 
            /// </summary> 
            /// <param name="data">要序列化的对象</param> 
            /// <returns>返回存放序列化后的数据缓冲区</returns> 
            public static byte[] Serialize(object data)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream rems = new MemoryStream())
                {
                    formatter.Serialize(rems, data);
                    rems.Close();
                    return rems.GetBuffer();
                }
            }

            /// <summary> 
            /// 反序列化 
            /// </summary> 
            /// <param name="data">数据缓冲区</param> 
            /// <returns>对象</returns> 
            public static object Deserialize(byte[] data)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream rems = new MemoryStream(data))
                {
                    object temp = formatter.Deserialize(rems);
                    rems.Close();
                    return temp;
                }
            }

            /// <summary>
            /// 压缩字节数组
            /// </summary>
            /// <param name="str"></param>
            public static byte[] Compress(byte[] inputBytes)
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    using (GZipStream zipStream = new GZipStream(outStream, CompressionMode.Compress, true))
                    {
                        zipStream.Write(inputBytes, 0, inputBytes.Length);
                        zipStream.Close(); //很重要，必须关闭，否则无法正确解压
                        return outStream.ToArray();
                    }
                }
            }

            /// <summary>
            /// 解压缩字节数组
            /// </summary>
            /// <param name="str"></param>
            public static byte[] Decompress(byte[] inputBytes)
            {

                using (MemoryStream inputStream = new MemoryStream(inputBytes))
                {
                    using (MemoryStream outStream = new MemoryStream())
                    {
                        using (GZipStream zipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                        {
                            zipStream.CopyTo(outStream);
                            zipStream.Close();
                            return outStream.ToArray();
                        }
                    }
                }
            }

            #endregion

            #region 文件操作
            /// <summary>
            /// 打开文件
            /// Process用using包起来
            /// </summary>
            public static Process OpenFile(string filePath, string startArgs = "")
            {
                Process process = new Process();
                process.StartInfo.FileName = filePath;
                process.StartInfo.Arguments = startArgs;
                // process.StartInfo.UseShellExecute = false;
                // process.StartInfo.RedirectStandardOutput = true;
                // process.StartInfo.RedirectStandardError = true;
                // process.StartInfo.CreateNoWindow = true;
                process.Start();
                // process.WaitForExit();
                // string output = process.StandardOutput.ReadToEnd();
                // string error = process.StandardError.ReadToEnd();
                return process;
            }

            /// <summary>
            /// 创建文件夹
            /// </summary>
            public static void CreateFolder(string path, CreatType type = CreatType.KeepOld)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                else
                {
                    switch (type)
                    {
                        case CreatType.CreateNew:
                            DeleteFolder(path);
                            Directory.CreateDirectory(path);
                            break;
                    }
                }
            }

            /// <summary>
            /// 创建文件
            /// </summary>
            public static void CreateFile(string path, CreatType type = CreatType.KeepOld)
            {
                if (!File.Exists(path))
                {
                    string dirPath = Utility.Text.SplitPathName(path)[0];
                    CreateFolder(dirPath);
                    File.Create(path).Close();
                }
                else
                {
                    switch (type)
                    {
                        case CreatType.CreateNew:
                            File.Delete(path);
                            File.Create(path).Close();
                            break;
                    }
                }
            }

            /// <summary>
            /// 删除文件夹
            /// </summary>
            public static void DeleteFolder(string dirPath)
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath);
            }

            /// <summary>
            /// 清空一个文件夹
            /// </summary>
            /// <param name="fullPath">文件夹路径</param>
            public static void ClearDirectory(string fullPath)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(fullPath);
                if (directoryInfo.Exists)
                {
                    FileSystemInfo[] fileSysInfo = directoryInfo.GetFileSystemInfos();
                    foreach (FileSystemInfo fsi in fileSysInfo)
                    {
                        if (fsi is DirectoryInfo)
                        {
                            Directory.Delete(fsi.FullName, true);
                        }
                        else
                        {
                            File.Delete(fsi.FullName);
                        }
                    }
                }
            }

            public static IEnumerable<FileSystemInfo> Foreach(string fullPath)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(fullPath);
                return Foreach(directoryInfo);
            }

            public static IEnumerable<FileSystemInfo> Foreach(DirectoryInfo directoryInfo)
            {
                if (directoryInfo.Exists)
                {
                    FileSystemInfo[] fileSysInfo = directoryInfo.GetFileSystemInfos();
                    foreach (FileSystemInfo fsi in fileSysInfo)
                    {
                        if (fsi is DirectoryInfo info)
                        {
                            yield return fsi;
                            foreach (var item in Foreach(info))
                            {
                                yield return item;
                            }
                        }
                        else
                        {
                            yield return fsi;
                        }
                    }
                }
            }

            #endregion


            public static void Test()
            {
                string[] drives = System.Environment.GetLogicalDrives();
                foreach (var item in drives)
                {
                    //Debug.Log(item);
                }
            }
        }
    }
}