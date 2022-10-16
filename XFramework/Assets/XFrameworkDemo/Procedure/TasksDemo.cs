using UnityEngine;
using XFramework;
using XFramework.Tasks;

public class TasksDemo : ProcedureBase
{
    public override void OnEnter(params object[] parms)
    {
        SingleTask singleTask = new SingleTask(() =>
        {
            Debug.Log("任务开始");
            return true;
        });

        singleTask.All(
            () => { return TimeTo(0.1f); },
            () => { return TimeTo(0.2f); },
            () => { return TimeTo(0.4f); },
            () => { return TimeTo(0.35f); },
            () => { return TimeTo(0.3f); }
        ).Then(
            () => { return TimeTo(0.5f); }
        ).Race(
            () => { return TimeTo(0.6f); },
            () => { return TimeTo(0.8f); },
            () => { return TimeTo(0.7f); },
            () => { return TimeTo(0.66f); },
            () => { return TimeTo(0.9f); }
        ).Then(
            () => { return TimeTo(1.0f); }
        );

        singleTask.Start();
    }

    private bool TimeTo(float time)
    {
        if (Time.time > time)
        {
            Debug.Log($"时间 {time} 到");
            return Time.time > time;
        }
        return false;
    }
}
