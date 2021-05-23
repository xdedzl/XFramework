﻿using UnityEngine;
using XFramework.UI;
using System.Text.RegularExpressions;

public class SettingPanel : PanelBase {

    Regex regex = new Regex("^[0-9]*$");

    public override void Reg()
    {
        Level = 10;

        (this["ConfirmBtn"] as GUButton).AddListener(() =>
        {
            int width, height;
            if (!regex.IsMatch((this["Width"] as GUInputField).inputField.text) || !regex.IsMatch((this["Height"] as GUInputField).inputField.text))
            {
                Debug.Log("请输出数字");
                return;
            }

            width = int.Parse((this["Width"] as GUInputField).inputField.text);
            height = int.Parse((this["Height"] as GUInputField).inputField.text);

            bool isFullScreen = (this["FullScreen"] as GUToggle).toggle.isOn;

            Screen.SetResolution(width, height, isFullScreen);
        });

        (this["Esc"] as GUButton).button.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        });

        (this["Back"] as GUButton).button.onClick.AddListener(() =>
        {
            UIHelper.Instance.Close(UIName.Setting);
        });
    }

    public override void OnOpen(params object[] args)
    {
        gameObject.SetActive(true);
    }

    public override void OnClose()
    {
        gameObject.SetActive(false);
    }
}
