using System;

namespace XFramework.Fsm
{
    internal interface IManagedFsm : IFsmInspectable, IDisposable
    {
        void Update();
    }
}
