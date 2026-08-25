using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace XFramework.Command
{
    enum MessageSource
    {
        XCommand = 0,
        Unity = 1
    }

    public partial class XCommand
    {
        private static UdpClient client;
        private static UdpClient sendClient;
        private static IPEndPoint hunterEndPoint;
        private static readonly ConcurrentQueue<string> s_PendingHunterCommands = new ConcurrentQueue<string>();
        private static XCommandHunterRunner s_HunterRunner;
        private static readonly string HUNTER_IP = "192.168.199.105";
        private static readonly string HUNTER_PORT = "10001";

        private static readonly int GAME_PORT = 10002;
        private static readonly Dictionary<LogType, MessageType> LogType_To_MessageType = new Dictionary<LogType, MessageType>
        {
            {LogType.Log, MessageType.NORMAL },
            {LogType.Warning, MessageType.WARNING },
            {LogType.Error, MessageType.ERROR },
            {LogType.Assert, MessageType.ERROR},
            {LogType.Exception, MessageType.ERROR }
        };

        public static bool IsHunterEnable
        {
            get
            {
                return client != null;
            }
        }

        /// <summary>
        /// 连接hunter
        /// </summary>
        public static void ConnetHunter()
        {
            if (client != null)
                return;

            IPAddress remoteIP = IPAddress.Parse(HUNTER_IP);
            hunterEndPoint = new IPEndPoint(remoteIP, Convert.ToInt32(HUNTER_PORT));

            client = new UdpClient(GAME_PORT);
            sendClient = new UdpClient(0);

            LogMessageReceived += OnLogMessageReceived;
            Application.logMessageReceived += OnUnityLogMessageReceived;
            EnsureHunterRunner();

            SendInitData();
            AsyncReceive();
        }

        /// <summary>
        /// 断开hunter
        /// </summary>
        public static void DisConnetHunter()
        {
            if (client is null)
                return;

            client.Close();
            client.Dispose();
            client = null;
            sendClient.Close();
            sendClient.Dispose();
            sendClient = null;
            hunterEndPoint = null;
            LogMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived -= OnUnityLogMessageReceived;
            if (s_HunterRunner != null)
            {
                UnityEngine.Object.Destroy(s_HunterRunner.gameObject);
                s_HunterRunner = null;
            }
            while (s_PendingHunterCommands.TryDequeue(out _))
            {
            }
        }

        private static void EnsureHunterRunner()
        {
            if (s_HunterRunner != null)
                return;
            var gameObject = new GameObject("XCommand Hunter Runner") {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            s_HunterRunner = gameObject.AddComponent<XCommandHunterRunner>();
        }

        static async void AsyncReceive()
        {
            UdpClient receiveClient = client;
            if (receiveClient == null)
                return;
            UdpReceiveResult result;
            try
            {
                result = await receiveClient.ReceiveAsync();
            }
            catch (Exception e)
            {
                if (!(e is SocketException) && !(e is ObjectDisposedException))
                    Debug.LogException(e);
                return;
            }
            if (receiveClient != client)
                return;
            OnHunterMessageRecived(result.Buffer);
            AsyncReceive();

        }

        private static void SendInitData()
        {
            HunterPacketWriter writer = new HunterPacketWriter();
            writer.AddInt32(-1);
            writer.AddInt32(-1);
            writer.AddString(GetLocalIP());

            var buffer = writer.Encode();
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnLogMessageReceived(Message message)
        {
            HunterPacketWriter writer = new HunterPacketWriter();
            writer.AddInt32((int)message.type);
            writer.AddInt32((int)MessageSource.XCommand);
            writer.AddString(message.text);

            var buffer = writer.Encode();
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var messageType = LogType_To_MessageType[type];

            HunterPacketWriter writer = new HunterPacketWriter();
            writer.AddInt32((int)messageType);
            writer.AddInt32((int)MessageSource.Unity);
            string message = messageType != MessageType.ERROR ? condition : $"{condition}\n{stackTrace}";
            writer.AddString(message);
            var buffer = writer.Encode();
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnHunterMessageRecived(byte[] buffer)
        {
            if (buffer is null)
                return;
            var command = Encoding.UTF8.GetString(buffer);
            if (!string.IsNullOrEmpty(command))
                s_PendingHunterCommands.Enqueue(command);
        }

        private static void ExecutePendingHunterCommands()
        {
            while (s_PendingHunterCommands.TryDequeue(out string command))
                Execute(command, out _, XCommandSource.Hunter);
        }

        private sealed class XCommandHunterRunner : MonoBehaviour
        {
            private void Update()
            {
                ExecutePendingHunterCommands();
            }
        }

        public static string GetLocalIP()
        {
            //获取主机名
            string HostName = Dns.GetHostName();
            IPHostEntry IpEntry = Dns.GetHostEntry(HostName);
            for (int i = 0; i < IpEntry.AddressList.Length; i++)
            {
                //从IP地址列表中筛选出IPv4类型的IP地址
                //AddressFamily.InterNetwork表示此IP为IPv4,
                //AddressFamily.InterNetworkV6表示此地址为IPv6类型
                if (IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return IpEntry.AddressList[i].ToString();
                }
            }
            return "";
        }

        private sealed class HunterPacketWriter
        {
            private readonly List<byte> buffer = new List<byte>();

            public void AddInt32(int value)
            {
                buffer.Add((byte)value);
                buffer.Add((byte)(value >> 8));
                buffer.Add((byte)(value >> 16));
                buffer.Add((byte)(value >> 24));
            }

            public void AddString(string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                AddInt32(bytes.Length);
                buffer.AddRange(bytes);
            }

            public byte[] Encode()
            {
                return buffer.ToArray();
            }
        }
    }

}
