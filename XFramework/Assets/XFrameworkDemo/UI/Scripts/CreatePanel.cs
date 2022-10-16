using UnityEngine;
using System.Threading.Tasks;
using System;
using XFramework.UI;
using UnityEngine.UI;

/// <summary>
/// 创建单位按钮
/// </summary>
public class CreatePanel : PanelBase {

    [HideInInspector] public Button groupBtn;        // 五群按钮
    [HideInInspector] public Button teamBtn;         // 十八队按钮
    [HideInInspector] public Button commandPostBtn;  // 三所按钮
     
    private const float groupHeight = 420;
    private const float teamHeight = 533;
    private const float comandPosHeight = 336;

    protected CanvasGroup canvasGroup;

    public override void Reg()
    {
        Level = 2;
        // 按钮赋值
        groupBtn = ((this["GroupBtn"] as XButton).button) as Button;
        teamBtn = ((this["TeamBtn"] as XButton).button) as Button;
        commandPostBtn = ((this["CommandPostBtn"] as XButton).button) as Button;

        // 注册鼠标点击事件
        groupBtn.onClick.AddListener(() => { OnClick(UIName.Group); });
        teamBtn.onClick.AddListener(() => { OnClick(UIName.Team); });
        commandPostBtn.onClick.AddListener(() => { OnClick(UIName.CommandPost); });
    }

    /// <summary>
    /// 界面被显示出来
    /// </summary>
    public override void OnOpen(params object[] args)
    {
        if (canvasGroup == null)
            canvasGroup = transform.GetComponent<CanvasGroup>();
        gameObject.SetActive(true);
        canvasGroup.interactable = true;
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// 界面不显示,退出这个界面，界面被关闭
    /// </summary>
    public override void OnClose()
    {
        gameObject.SetActive(false);
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// 点击事件
    /// </summary>
    /// <param name="panelType"></param>
    private void OnClick(string panelType)
    {
        UIHelper.Instance.Open(panelType);
        

        Vector2 rectSize = rect.sizeDelta;
        switch (panelType)
        {
            case UIName.Group:
                rectSize.y = groupHeight;
                break;
            case UIName.Team:
                rectSize.y = teamHeight;
                break;
            case UIName.CommandPost:
                rectSize.y = comandPosHeight;
                break;
            default:
                break;
        }
        rect.sizeDelta = rectSize;
    }
}
