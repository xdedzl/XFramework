using UnityEngine;
using XFramework.UI;

/// <summary>
/// 五群界面
/// </summary>
public class GroupPanel : PanelBase {

    private Vector2 rectSize;
    protected CanvasGroup canvasGroup;

    public override void Reg()
    {
        Level = 3;
        rectSize = rect.sizeDelta;
        

        Vector2 size = rect.sizeDelta;
        size.y = 1.5f;
        rect.sizeDelta = size;
    }

    /// <summary>
    /// 进入该按钮状态
    /// </summary>
    public override void OnOpen(params object[] args)
    {
        CreatePanel createPanel = (CreatePanel)UIHelper.Instance.GetPanel(UIName.Create);
        // 设父物体以及自己在子物体中的顺序
        transform.SetParent(createPanel.groupBtn.transform.parent, true);
        transform.SetSiblingIndex(createPanel.groupBtn.transform.GetSiblingIndex() + 1);
        if (canvasGroup == null)
            canvasGroup = transform.GetComponent<CanvasGroup>();
        gameObject.SetActive(true);
        canvasGroup.interactable = true;
    }

    /// <summary>
    /// 退出该按钮状态
    /// </summary>
    public override void OnClose()
    {
        gameObject.SetActive(false);
        canvasGroup.interactable = false;
    }
}
