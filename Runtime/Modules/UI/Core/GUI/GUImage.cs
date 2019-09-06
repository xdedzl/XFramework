﻿using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Image))]
    public class GUImage : GUIBase
    {
        public UnityEngine.UI.Image image;

        private void Reset()
        {
            image = transform.GetComponent<UnityEngine.UI.Image>();
        }
    }
}