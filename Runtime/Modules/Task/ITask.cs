using System;

namespace XFramework.Tasks
{
    public interface ITask
    {
        bool IsDone { get; set; }
        ITask Next { get; set; }

        void Update();
        ITask Then(ITask task);
        ITask Then(Func<bool> func);
        ITask All(params ITask[] task);
        ITask Race(params ITask[] tasks);
        ITask Race(params Func<bool>[] funcs);

    }
}