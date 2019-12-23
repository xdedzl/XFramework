using System;
using System.Collections.Generic;
using System.IO;
using XFramework.Resource;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    public enum PackOption
    {
        AllFiles,       // 所有文件一个包 
        TopDirectiony,  // 一级子文件夹单独打包
        AllDirectiony,  // 所有子文件夹单独打包
        TopFileOnly,    // 只打包当前文件夹的文件
    }

    /// <summary>
    /// AssetBundle窗口
    /// </summary>
    public partial class AssetBundleEditor : EditorWindow
    {
        private enum TabMode
        {
            Default,
            Dependence,
            Mainfest2Json,
            BuildProject,
        }

        [MenuItem("SFramework/Resource/AssetBundleWindow")]
        static void OpenWindow()
        {
            var window = GetWindow(typeof(AssetBundleEditor));
            window.titleContent = new GUIContent("AssetBundle");
            window.Show();
            window.minSize = new Vector2(400, 100);
        }

        private TabMode m_TabMode = TabMode.BuildProject;

        public SubWindow[] m_SubWindows = new SubWindow[]
        {
            new DefaultTab(),
            new DependenceTab(),
            new Mainfest2Json(),
            new BuildTab(),
        };

        private void OnEnable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnEnable();
            }
        } 

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_TabMode = (TabMode)GUILayout.Toolbar((int)m_TabMode, Enum.GetNames(typeof(TabMode)));
                }

                GUILayout.Space(10);
                m_SubWindows[(int)m_TabMode].OnGUI();
            }
        } 

        private void OnDisable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnDisable();
            }
        }

        #region 

        /// <summary>
        /// 将unity依赖转为自己的
        /// </summary>
        /// <param name="mainfest"></param>
        /// <returns></returns>
        public static DependenciesData GenerateDependence(AssetBundleManifest mainfest)
        {
            string[] abNames = mainfest.GetAllAssetBundles();

            List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

            for (int j = 0; j < abNames.Length; j++)
            {
                var dpNames = mainfest.GetDirectDependencies(abNames[j]);
                if (dpNames.Length <= 0)
                {
                    continue;
                }
                singleDatas.Add(new SingleDepenciesData(abNames[j], dpNames));
            }
            var data = new DependenciesData(singleDatas.ToArray());
            return data;
        }

        #endregion

    }
}