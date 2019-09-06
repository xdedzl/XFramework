using UnityEngine;
using XFramework.UI;

public class MainPanel : PanelBase {

    public override void Reg()
    {
        Level = 1;
        (this["CreateBtn"] as GUButton).button.onClick.AddListener(() => { OnClick(UIName.Create); });
        (this["PowerBtn"] as GUButton).button.onClick.AddListener(() => { OnClick(UIName.ShowPower); });
        (this["AdjustBtn"] as GUButton).button.onClick.AddListener(() => { OnClick(UIName.Adjust); });
    }

    /// <summary>
    /// 处理各个按钮的点击事件
    /// </summary>
    /// <param name="_type"></param>
    private void OnClick(string _type)
    {
        Game.UIModule.Open(_type);
    }
}