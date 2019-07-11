using System;

namespace XFramework.Tasks
{
    public interface ITask
    {
        bool IsDone { get; set; }
        ITask Next { get; set; }

        void Update();
    }
}