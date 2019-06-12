using System;
using Net.Common;
using Net.Core;
using Net.Logic;

namespace Net
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server(100);
            server.Start("127.0.0.1", 2048);

            server.AddListener(1, TestJson.PrintData);

            ProtocolBytes p = new ProtocolBytes();
            p.AddInt32(1); 
            p.AddInt32(1);
            

            while (true)
            {
                string str = Console.ReadLine();
                switch (str)
                {
                    case "quit":
                        server.Close();
                        return;
                    case "print":
                        server.Broadcast(1, p.Encode());
                        break;
                }
            }
        }
    }
}