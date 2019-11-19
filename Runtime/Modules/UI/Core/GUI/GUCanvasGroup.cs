﻿using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(CanvasGroup))]

    public class GUCanvasGroup : GUIBase
    {
        public CanvasGroup canvasGroup;

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}