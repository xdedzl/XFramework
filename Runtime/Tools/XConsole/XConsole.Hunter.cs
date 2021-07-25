using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace XFramework.Console
{
    public partial class XConsole
    {
        public static UdpClient client;
        public static IPEndPoint hunterEndPoint;
        static readonly string HUNTER_IP = "127.0.0.1";
        static readonly string HUNTER_PORT = "10001";

        static readonly string IP = "127.0.0.1";
        static readonly int PORT = 10002;

        public static void ConnetHunter()
        {
            if (client != null)
                return;

            IPAddress remoteIP = IPAddress.Parse(HUNTER_IP);
            hunterEndPoint = new IPEndPoint(remoteIP, Convert.ToInt32(HUNTER_PORT));

            client = new UdpClient(PORT);

            LogMessageReceived += OnLogMessageReceived;
            Application.logMessageReceived += OnUnityLogMessageReceived;

            AsyncReceive();
        }

        static async void AsyncReceive()
        {
            string strs = "";
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync();
                strs = Encoding.UTF8.GetString(result.Buffer);
            }
            catch (Exception e)
            {
                if (!(e is SocketException))
                {
                    return;
                }
            }
            AsyncReceive();
            if (!string.IsNullOrEmpty(strs))
            {
                OnHunterMessageRecived(strs);
            }
        }

        public static void DisConnetHunter()
        {
            if (client is null)
                return;

            client.Close();
            client.Dispose();
            client = null;
            LogMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived -= OnUnityLogMessageReceived;
        }

        private static void OnLogMessageReceived(string content)
        {
            byte[] sendData = Encoding.UTF8.GetBytes(content);
            client.Send(sendData, sendData.Length, hunterEndPoint);
        }

        private static void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            byte[] sendData = Encoding.UTF8.GetBytes(condition);
            client.Send(sendData, sendData.Length, hunterEndPoint);
        }

        private static void OnHunterMessageRecived(string cmd)
        {

        }
    }

}
