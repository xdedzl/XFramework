using UnityEngine;
using XFramework.Event;

/*
 * 以战斗系统为例
 */

//数据类型枚举
public enum DataType
{
    BATTLE = 0,
}

/// <summary>
/// 挂在场景中
/// </summary>
public class ObserverExample : MonoBehaviour
{
    BattleSystem bat;
    PlayerDataMgr pd;

    void Start()
    {
        // 正常使用时观察者和被观察者都不应当是直接New
        bat = new BattleSystem();
        pd = new PlayerDataMgr();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            bat.BattleWin();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            bat.BattleLose();
        }
    }
}

#region 战斗模块  
/*
 * 一个可被观察者观察的主题分为三个部分
 * 1.主题管理类
 * 2.派生数据类
 * 3.可被观察的事件类型
 */

public class BattleSystem
{
    /// <summary>
    /// data由各个主题各自管理，可能存在多分，也可能全局只有一个
    /// </summary>
    BattleData btData = new BattleData();

    public void BattleWin()
    {
        // 处理战斗胜利要做的事情
        // 。。。。。。。。。。。。。

        // 通知所有观察者并传递数据
        DataSubjectManager.Instance.Notify(btData, (int)BattleDataType.Win);
    }

    public void BattleLose()
    {
        // 处理战斗失败要做的事情
        // 。。。。。。。。。。。。。

        DataSubjectManager.Instance.Notify(btData, (int)BattleDataType.Lose);
    }
}

public class BattleData : EventData
{
    public int battleCount;
    public int loseCount;
    public int winCount;

    public BattleData() : base()
    {
        battleCount = 5;
        loseCount = 3;
        winCount = 2;
    }

    public override int dataType
    {
        get
        {
            return (int)DataType.BATTLE;
        }
    }
}

public enum BattleDataType
{
    Win = 0,
    Lose = 1,
}

#endregion

/// <summary>
/// 观察者
/// </summary>
public class PlayerDataMgr : IObserver
{
    // 在合适的时添加除观察者
    public PlayerDataMgr()
    {
        DataSubjectManager.Instance.AddListener(DataType.BATTLE, this);
    }

    // 在合适的时候移除观察者
    ~PlayerDataMgr()
    {
        DataSubjectManager.Instance.RemoverListener(DataType.BATTLE, this);
    }

    // 固定写法
    public void OnDataChange(EventData eventData, int type, object obj)
    {
        switch (eventData.dataType)
        {
            case (int)DataType.BATTLE:
                BattleData data = eventData as BattleData;
                switch (type)
                {
                    case (int)BattleDataType.Win:
                        Debug.Log("total" + data.battleCount + "   win" + data.winCount);
                        break;
                    case (int)BattleDataType.Lose:
                        Debug.Log("total" + data.battleCount + "   lose" + data.loseCount);
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }
}