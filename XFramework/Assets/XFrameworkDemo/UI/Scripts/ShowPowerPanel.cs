using UnityEngine;
using DG.Tweening;
using XFramework.UI;

public class ShowPowerPanel : BasePanel {
    protected CanvasGroup canvasGroup;
    public override void Reg()
    {
        Level = 2;
    }

    /// <summary>
    /// 界面被显示出来
    /// </summary>
    public override void OnOpen()
    {
        if (canvasGroup == null)
            canvasGroup = transform.GetComponent<CanvasGroup>();
        rect.DOScaleY(1.0f, 0.1f);
        canvasGroup.interactable = true;
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// 界面不显示,退出这个界面，界面被关闭
    /// </summary>
    public override void OnClose()
    {
        rect.DOScaleY(0, 0.1f);
        canvasGroup.interactable = false;
    }
}
