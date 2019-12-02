using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 用于创建一些自定义UI组件
/// </summary>
public class CreateComponent
{
    private static DefaultControls.Resources s_StandardResources;

    private const string kUILayerName = "UI";

    private const string kStandardSpritePath = "UI/Skin/UISprite.psd";
    private const string kBackgroundSpritePath = "UI/Skin/Background.psd";
    private const string kInputFieldBackgroundPath = "UI/Skin/InputFieldBackground.psd";
    private const string kKnobPath = "UI/Skin/Knob.psd";
    private const string kCheckmarkPath = "UI/Skin/Checkmark.psd";
    private const string kDropdownArrowPath = "UI/Skin/DropdownArrow.psd";
    private const string kMaskPath = "UI/Skin/UIMask.psd";

    [MenuItem("GameObject/UI/Tree")]
    public static void CreateTree()
    {
        GameObject parent = Selection.activeGameObject;

        RectTransform tree = new GameObject("Tree").AddComponent<RectTransform>();
        tree.SetParent(parent.transform);
        tree.localPosition = Vector3.zero;
        tree.gameObject.AddComponent<XFramework.UI.Tree>();
        tree.sizeDelta = new Vector2(180, 30);

        // 设置模板
        RectTransform itemTemplate = new GameObject("NodeTemplate").AddComponent<RectTransform>();
        itemTemplate.SetParent(tree);
        itemTemplate.pivot = new Vector2(0, 1);
        itemTemplate.anchorMin = new Vector2(0, 1);
        itemTemplate.anchorMax = new Vector2(0, 1);
        itemTemplate.anchoredPosition = new Vector2(0, 0);
        itemTemplate.sizeDelta = new Vector2(180, 30);

        RectTransform body = DefaultControls.CreateButton(GetStandardResources()).GetComponent<RectTransform>();
        body.name = "Body";
        body.SetParent(itemTemplate);
        body.anchoredPosition = new Vector2(10, 0);
        body.sizeDelta = new Vector2(160, 30);
        Object.DestroyImmediate(body.GetComponent<Button>());
        body.gameObject.AddComponent<Toggle>();
        body.GetComponentInChildren<Text>().text = "Root";

        //RectTransform toggle = DefaultControls.CreateToggle(GetStandardResources()).GetComponent<RectTransform>();
        //toggle.SetParent(itemTemplate);
        //Object.DestroyImmediate(toggle.Find("Label").gameObject);
        //toggle.anchoredPosition = new Vector2(-80, 0);
        //toggle.sizeDelta = new Vector2(20, 20);

        RectTransform toggle = DefaultControls.CreateImage(GetStandardResources()).GetComponent<RectTransform>();
        toggle.name = "Toggle";
        toggle.SetParent(itemTemplate);
        toggle.anchoredPosition = new Vector2(-80, 0);
        toggle.sizeDelta = new Vector2(20, 20);
        toggle.gameObject.AddComponent<Toggle>();

        RectTransform child = new GameObject("Child").AddComponent<RectTransform>();
        child.SetParent(itemTemplate);
        child.pivot = new Vector2(0, 1);
        child.anchorMin = new Vector2(0, 1);
        child.anchorMax = new Vector2(0, 1);
        child.sizeDelta = Vector2.zero;
        child.anchoredPosition = new Vector2(20, -30);


        // 设置树的跟结点位置
        RectTransform treeRoot = new GameObject("Root").AddComponent<RectTransform>();
        treeRoot.SetParent(tree);
        treeRoot.pivot = new Vector2(0, 1);
        treeRoot.anchorMin = new Vector2(0, 1);
        treeRoot.anchorMax = new Vector2(0, 1);
        treeRoot.anchoredPosition = new Vector2(0, 0);
        treeRoot.sizeDelta = new Vector2(0, 0);
    }

    [MenuItem("GameObject/UI/SliderMixInput")]
    public static void CreateSliderMixInput()
    {
        GameObject parent = Selection.activeGameObject;

        RectTransform mix = new GameObject("SliderMixInput").AddComponent<RectTransform>();
        mix.SetParent(parent.transform);
        mix.anchoredPosition = Vector2.zero;
        mix.sizeDelta = new Vector2(210, 25);

        // 设置Text
        RectTransform text = DefaultControls.CreateText(GetStandardResources()).GetComponent<RectTransform>();
        text.SetParent(mix);
        text.name = "Name";
        text.anchorMin = new Vector2(0, 0.5f);
        text.anchorMax = new Vector2(0, 0.5f);
        text.sizeDelta = new Vector2(50, 25);
        text.anchoredPosition = new Vector2(25, 0);
        text.GetComponent<Text>().text = "Name";
        text.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        // 设置滑动条
        RectTransform slider = DefaultControls.CreateSlider(GetStandardResources()).GetComponent<RectTransform>();
        slider.SetParent(mix);
        slider.sizeDelta = new Vector2(80, 20);
        slider.anchoredPosition = new Vector2(-15, 0);

        // 设置输入框
        RectTransform input = DefaultControls.CreateInputField(GetStandardResources()).GetComponent<RectTransform>();
        input.SetParent(mix);
        input.anchorMin = new Vector2(1, 0.5f);
        input.anchorMax = new Vector2(1, 0.5f);
        input.sizeDelta = new Vector2(80, 20);
        input.anchoredPosition = new Vector2(-40, 0);

        mix.gameObject.AddComponent<XFramework.UI.SliderMixInput>();
    }

    private static DefaultControls.Resources GetStandardResources()
    {
        if (s_StandardResources.standard == null)
        {
            s_StandardResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>(kStandardSpritePath);
            s_StandardResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>(kBackgroundSpritePath);
            s_StandardResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>(kInputFieldBackgroundPath);
            s_StandardResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>(kKnobPath);
            s_StandardResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>(kCheckmarkPath);
            s_StandardResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>(kDropdownArrowPath);
            s_StandardResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>(kMaskPath);
        }
        return s_StandardResources;
    }
}