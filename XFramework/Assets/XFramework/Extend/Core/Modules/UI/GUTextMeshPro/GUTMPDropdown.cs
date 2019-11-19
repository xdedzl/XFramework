using System.Collections.Generic;
using TMPro;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(TMP_Dropdown))]
    public class GUTMPDropdown : GUIBase
    {
        public TMP_Dropdown dropdown;

        public int Value
        {
            get
            {
                if (dropdown != null)
                {
                    return dropdown.value;
                }
                else
                {
                    return -1;
                }
            }

            set
            {
                if (dropdown != null)
                {
                    dropdown.value = value;
                }
            }
        }

        /// <summary>
        /// 根据字符串内容设置下拉框的值
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public bool SetValue(string content)
        {
            int index = GetIndex(content);
            if (index >= 0)
            {
                Value = index;
                return true;
            }
            return false;
        }

        private void Reset()
        {
            dropdown = transform.GetComponent<TMP_Dropdown>();
        }

        public void AddListener(UnityEngine.Events.UnityAction<int> call)
        {
            dropdown.onValueChanged.AddListener(call);
        }

        /// <summary>
        /// 返回当前选中项的文字
        /// </summary>
        public string CurrText
        {
            get
            {
                if (dropdown.options.Count > 0)
                {
                    return dropdown.options[dropdown.value].text;
                }
                return null;
            }
            set
            {
                if (dropdown.options.Count > 0)
                {
                    dropdown.options[dropdown.value].text = value;
                }
            }
        }

        public List<TMP_Dropdown.OptionData> Options
        {
            get
            {
                return dropdown.options;
            }
            set
            {
                dropdown.options = value;
            }
        }

        /// <summary>
        /// 获取对应索引处的字符串
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns></returns>
        public string GetContent(int index)
        {
            var values = dropdown.options;
            if (index >= values.Count || index < 0)
            {
                return "";
            }
            else
            {
                return values[index].text;
            }
        }

        /// <summary>
        /// 获取对应内容的index
        /// </summary>
        /// <param name="content"></param>
        public int GetIndex(string content)
        {
            var options = dropdown.options;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].text == content)
                {
                    return i;
                }
            }

            return -1;
        }

        public void ClearOptions()
        {
            dropdown.ClearOptions();
        }

        public void AddOptions(List<string> options)
        {
            dropdown.AddOptions(options);
        }
    }
}