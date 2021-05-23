using UnityEngine;
using XFramework.UI;

public class CommandPostPanel : PanelBase {

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

    public override void OnOpen(params object[] args)
    {
        CreatePanel createPanel = (CreatePanel)UIHelper.Instance.GetPanel(UIName.Create);
        // 设父物体以及自己在子物体中的顺序
        transform.SetParent(createPanel.commandPostBtn.transform.parent, true);
        transform.SetSiblingIndex(createPanel.commandPostBtn.transform.GetSiblingIndex() + 1);

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
