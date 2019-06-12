using UnityEngine;
using DG.Tweening;
using XFramework.UI;

public class CommandPostPanel : BasePanel {

    private Vector2 rectSize;
    protected CanvasGroup canvasGroup;

    // Use this for initialization
    public override void Reg()
    {
        Level = 3;
        rectSize = rect.sizeDelta;

        Vector2 size = rect.sizeDelta;
        size.y = 1.5f;
        rect.sizeDelta = size;
    }

    public override void OnOpen()
    {
        CreatePanel createPanel = (CreatePanel)Game.UIModule.GetPanel(UIName.Create);
        // 设父物体以及自己在子物体中的顺序
        transform.SetParent(createPanel.commandPostBtn.transform.parent, true);
        transform.SetSiblingIndex(createPanel.commandPostBtn.transform.GetSiblingIndex() + 1);

        if (canvasGroup == null)
            canvasGroup = transform.GetComponent<CanvasGroup>();
        rect.DOSizeDelta(rectSize, 0.3f); // 进场动画
        canvasGroup.interactable = true;
    }

    public override void OnClose()
    {
        rect.DOSizeDelta(new Vector2(rectSize.x, 1.5f), 0.3f); // 退出动画
        canvasGroup.interactable = false;
    }
}
