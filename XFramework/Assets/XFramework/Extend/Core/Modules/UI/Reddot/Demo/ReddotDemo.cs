using UnityEngine;
using UnityEngine.UI;
using XFramework.UI;

public class ReddotDemo : MonoBehaviour
{
    public GULayoutGroup toggles;
    void Start()
    {
        var keys = ReddotManager.Instance.GetLeafKeys();

        foreach (var key in keys)
        {
            var obj = toggles.AddEntity();

            Toggle toggle = obj.GetComponent<Toggle>();

            toggle.onValueChanged.AddListener((v) =>
            {
                ReddotManager.Instance.MarkReddot(key, v);
            });
            toggle.transform.Find("Label").GetComponent<Text>().text = key;
        }
    }
}
