using UnityEngine;
using XFramework;
using ProtocolBytes = Net.Common.ProtocolBytes;

public class NetTest : ProcedureBase
{
    public override void Init()
    {
        Game.NetModule.AddListener(1, (data) =>
        {
            ProtocolBytes r = new ProtocolBytes(data.data);
            Debug.Log(r.GetString());
        });


        ProtocolBytes s = new ProtocolBytes();
        s.AddFloat(10);
        s.AddString("hellow world");

        Game.NetModule.StartConnectAsync("127.0.0.1", 2048, () =>
        {
            Game.NetModule.Send(1, s.Encode());
        });
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            
        }
    }
}
