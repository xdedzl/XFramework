using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Net.Common;

namespace Net.Logic
{
    class TestJson
    {
        public static void PrintData(NetData data)
        {
            Console.WriteLine(data.dataType + "  ");
            ProtocolBytes r = new ProtocolBytes(data.data);
            Console.WriteLine(r.GetFloat());
            Console.WriteLine(r.GetString());

            ProtocolBytes s = new ProtocolBytes();
            s.AddInt32(data.dataType);
            s.AddString("我回来了");

            data.conn.Send(s.Encode());
        }
    }
}
