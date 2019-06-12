namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Button))]
    public class GUButton : BaseGUI
    {
        public UnityEngine.UI.Button button;

        private void Reset()
        {
            button = transform.GetComponent<UnityEngine.UI.Button>();
        }

        public void AddListener(UnityEngine.Events.UnityAction call)
        {
            button.onClick.AddListener(call);
        }
    }
}