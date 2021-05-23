using UnityEngine;
using XFramework.UI;

/// <summary>
/// 十八队界面
/// </summary>
public class TeamPanel : PanelBase {

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

    public override void OnOpen(params object[] args)
    {
        CreatePanel createPanel = (CreatePanel)UIHelper.Instance.GetPanel(UIName.Create);
        // 设父物体以及自己在子物体中的顺序
        transform.SetParent(createPanel.teamBtn.transform.parent, true);
        transform.SetSiblingIndex(createPanel.teamBtn.transform.GetSiblingIndex() + 1);
        if (canvasGroup == null)
            canvasGroup = transform.GetComponent<CanvasGroup>();
        gameObject.SetActive(true);
        canvasGroup.interactable = true;
    }

    public override void OnClose()
    {
        gameObject.SetActive(false);
        canvasGroup.interactable = false;
    }
}
