using UnityEngine;
using UnityEngine.UI;

public class RootButton : MonoBehaviour {

    private Button startBtn;

	void Start ()
    {
        Screen.SetResolution(1920, 1080, true);
        startBtn = GetComponent<Button>();
        startBtn.onClick.AddListener(OnClick);
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
	}

    public void OnClick()
    {
        // 显示主界面
        Game.UIModule.Open(UIName.Main);
    }

    private void Update()
    {
        // 打开/关闭设置界面
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Game.UIModule.Open(UIName.Setting);
        }
    }
}
