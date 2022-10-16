using UnityEngine;
using XFramework.UI;
public class AdjustPanel : PanelBase {
    protected CanvasGroup canvasGroup;

    public override void Reg()
    {
        Level = 2;
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
}
