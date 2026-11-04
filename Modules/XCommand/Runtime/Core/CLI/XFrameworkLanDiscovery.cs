using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace XFramework.Command
{
    internal sealed class XFrameworkLanDiscovery
    {
        private const int DISCOVERY_PORT = 48733;
        private const int ANNOUNCE_INTERVAL_MILLISECONDS = 2000;
        private readonly byte[] payload;
        private readonly Action<Exception> reportError;
        private readonly ManualResetEventSlim stopSignal = new ManualResetEventSlim(false);
        private UdpClient client;
        private Thread thread;
        private int stopped;

        public XFrameworkLanDiscovery(string announcementJson, Action<Exception> reportError)
        {
            payload = Encoding.UTF8.GetBytes(announcementJson);
            this.reportError = reportError;
        }

        public void Start()
        {
            try
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
                client.EnableBroadcast = true;
                client.Client.SendTimeout = 1000;
                Thread worker = new Thread(AnnounceLoop) {
                    IsBackground = true,
                    Name = "XFramework LAN Discovery",
                };
                worker.Start();
                thread = worker;
            }
            catch (Exception exception)
            {
                Stop();
                reportError(exception);
            }
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0)
            {
                return;
            }
            stopSignal.Set();
            client?.Close();
            if (thread == null || thread.Join(1000))
            {
                stopSignal.Dispose();
            }
        }

        private void AnnounceLoop()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
            bool errorReported = false;
            try
            {
                while (!stopSignal.IsSet)
                {
                    try
                    {
                        client.Send(payload, payload.Length, endpoint);
                    }
                    catch (Exception exception)
                    {
                        if (!stopSignal.IsSet && !errorReported)
                        {
                            reportError(exception);
                            errorReported = true;
                        }
                    }
                    if (stopSignal.Wait(ANNOUNCE_INTERVAL_MILLISECONDS))
                    {
                        break;
                    }
                }
            }
            finally
            {
                client.Close();
            }
        }
    }
}
