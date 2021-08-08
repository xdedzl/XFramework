using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace XFramework.Console
{
    enum MessageSource
    {
        XConsole = 0,
        Unity = 1
    }

    public partial class XConsole
    {
        private static UdpClient client;
        private static UdpClient sendClient;
        private static IPEndPoint hunterEndPoint;
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
            LogMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived -= OnUnityLogMessageReceived;
        }

        static async void AsyncReceive()
        {
            UdpReceiveResult result;
            try
            {
                result = await client.ReceiveAsync();
            }
            catch (Exception e)
            {
                if (!(e is SocketException))
                {
                    return;
                }
            }
            OnHunterMessageRecived(result.Buffer);
            AsyncReceive();

        }

        private static void SendInitData()
        {
            byte[] buffer = Encoding.UTF8.GetBytes(GetLocalIP());
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnLogMessageReceived(Message message)
        {
            ProtocolBytes protocolBytes = new ProtocolBytes();
            protocolBytes.AddInt32((int)message.type);
            protocolBytes.AddInt32((int)MessageSource.XConsole);
            protocolBytes.AddString(message.text);

            //List<byte> buffter = new List<byte>();
            //byte[] sendData = Encoding.UTF8.GetBytes(message.text);
            //byte[] logType = BitConverter.GetBytes((int)message.type);
            //byte[] sourceType = BitConverter.GetBytes((int)MessageSource.XConsole);
            //buffter.AddRange(logType);
            //buffter.AddRange(sourceType);
            //buffter.AddRange(sendData);
            var buffer = protocolBytes.Encode();
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var messageType = LogType_To_MessageType[type];

            ProtocolBytes protocolBytes = new ProtocolBytes();
            protocolBytes.AddInt32((int)messageType);
            protocolBytes.AddInt32((int)MessageSource.Unity);
            string message = messageType != MessageType.ERROR ? condition : $"{condition}\n{stackTrace}";
            protocolBytes.AddString(message);
            var buffer = protocolBytes.Encode();
            client.Send(buffer, buffer.Length, hunterEndPoint);
        }

        private static void OnHunterMessageRecived(byte[] buffer)
        {
            if (buffer is null)
                return;
            var command = Encoding.UTF8.GetString(buffer);
            if (!string.IsNullOrEmpty(command))
            {
                Excute(command);
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
    }

}
