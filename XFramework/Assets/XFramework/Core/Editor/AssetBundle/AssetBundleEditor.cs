using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

/// <summary>
/// 打包 AssetBundle
/// </summary>
public class AssetBundleEditor : EditorWindow
{
    public enum PackOption
    {
        AllFiles,       // 所有文件一个包 
        TopDirectiony,  // 一级子文件夹单独打包
        AllDirectiony,  // 所有子文件夹单独打包
    }

    public class PackInfo
    {
        public string path;
        public PackOption option;
    }

    [MenuItem("XFramework/ABWindiow")]
    static void BuildAssetBundle()
    {
        GetWindow(typeof(AssetBundleEditor)).Show();
    }

    private List<PackInfo> m_PackInfos;

    private string OutPutPath;

    private VisualElement m_RootContainter;
    private ScrollView ABItemList;

    private void OnEnable()
    {
        m_PackInfos = new List<PackInfo>();
        OutPutPath = Application.streamingAssetsPath + "/AssetBundles";

        rootVisualElement.Add(m_RootContainter = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,

                backgroundColor = Color.blue,
                opacity = 0.2f,

                paddingBottom = 5,
                paddingLeft = 5,
                paddingRight = 5,
                paddingTop = 5,
                fontSize = 13,
            }
        });

        m_RootContainter.Add(MenuBar());
        m_RootContainter.Add(ABItemList = new ScrollView());
        m_RootContainter.Add(BottomMenu());
    }

    // 菜单栏
    private VisualElement MenuBar()
    {
        VisualElement menuBar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                minHeight = 25,
            }
        };

        Button addAB = new Button(() =>
        {
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath("Assets/Terrains"))
            {
                Debug.Log(item);
            }

            if (Selection.objects != null)
            {
                foreach (var item in Selection.objects)
                {
                    PackInfo packInfo = new PackInfo
                    {
                        path = AssetDatabase.GetAssetPath(item),
                        option = PackOption.AllFiles,
                    };
                    m_PackInfos.Add(packInfo);

                    // 添加一个AB包条目
                    VisualElement abitem = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                        }
                    };

                    // 路径
                    TextElement textElement = new TextElement();
                    textElement.text = packInfo.path;
                    textElement.style.color = Color.blue;
                    textElement.style.width = 1000;

                    // 打包方式
                    EnumField enumField = new EnumField(packInfo.option);
                    enumField.RegisterValueChangedCallback((value) =>
                    {
                        packInfo.option = (PackOption)value.newValue;
                    });
                    enumField.style.marginLeft = 10;
                    enumField.style.width = 100;

                    // 删除路径
                    Button deleteBtn = new Button(() =>
                    {
                        m_PackInfos.Remove(packInfo);
                        ABItemList.Remove(abitem);
                    });
                    deleteBtn.text = "删除";
                    deleteBtn.style.marginLeft = 5;

                    abitem.Add(textElement);
                    abitem.Add(enumField);
                    abitem.Add(deleteBtn);

                    ABItemList.Add(abitem);
                }
            }
            else
            {
                Debug.LogWarning("请选择要添加的文件夹");
            }

        });
        addAB.text = "添加AB包";
        addAB.style.minWidth = 25;
        //addAB.style.

        menuBar.Add(addAB);

        return menuBar;
    }

    // 底边栏
    private VisualElement BottomMenu()
    {
        VisualElement bottomMenu = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                minHeight = 25,
            }
        };

        Button deleteBtn = new Button(() =>
        {
            if (!Directory.Exists(OutPutPath))
            {
                // 删除之前的ab文件
                FileInfo[] fs = new DirectoryInfo(OutPutPath).GetFiles("*", SearchOption.AllDirectories);
                foreach (var f in fs)
                {
                    f.Delete();
                }
            }
        });
        deleteBtn.text = "删除AB包";
        deleteBtn.style.minHeight = 25;

        // 标记
        Button markBtn = new Button(() =>
        {
            // 强制删除所有AssetBundle名称  
            string[] abNames = AssetDatabase.GetAllAssetBundleNames();
            for (int i = 0; i < abNames.Length; i++)
            {
                AssetDatabase.RemoveAssetBundleName(abNames[i], true);
            }

            foreach (var item in m_PackInfos)
            {
                Debug.Log(item.option);
                DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + item.path);
                MarkDirectory(info, item.option);
            }
        });
        markBtn.text = "标记";
        markBtn.style.marginLeft = 5;
        markBtn.style.minHeight = 25;

        Button packBtn = new Button(StartPack)
        {
            text = "打包"
        };
        packBtn.style.marginLeft = 5;
        packBtn.style.minHeight = 25;

        bottomMenu.Add(deleteBtn);
        bottomMenu.Add(markBtn);
        bottomMenu.Add(packBtn);

        return bottomMenu;
    }

    /// <summary>
    /// 标记文件
    /// </summary>
    /// <param name="path"></param>
    /// <param name="packOption"></param>
    private void MarkDirectory(DirectoryInfo dirInfo, PackOption packOption)
    {
        FileInfo[] files = null;
        DirectoryInfo[] subDirectory = null;
        switch (packOption)
        {
            case PackOption.AllFiles:
                files = dirInfo.GetFiles("*", SearchOption.AllDirectories);        // 取出所有文件
                break;
            case PackOption.TopDirectiony:
                files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                foreach (var item in subDirectory)
                {
                    MarkDirectory(item, PackOption.AllFiles);
                }
                break;
            case PackOption.AllDirectiony:
                files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                foreach (var item in subDirectory)
                {
                    MarkDirectory(item, PackOption.AllDirectiony);
                }
                break;
        }

        // 标记
        int total = files.Length;
        string abName = dirInfo.FullName.Substring(dirInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));
        for (int i = 0; i < total; i++)
        {
            var fileInfo = files[i];

            if (fileInfo.Name.EndsWith(".mata")) continue;

            EditorUtility.DisplayProgressBar(dirInfo.Name, $"正在标记资源{fileInfo.Name}...", (float)i / total);

            string filePath = fileInfo.FullName.Substring(fileInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));       // 获取 "Assets"目录起的 文件名, 可不用转 "\\"

            AssetImporter importer = AssetImporter.GetAtPath(filePath);     // 拿到该文件的 AssetImporter
            if (importer)
            {
                importer.assetBundleName = abName;
                importer.assetBundleVariant = "ab";
                //importer.SaveAndReimport();
            }
        }
        EditorUtility.ClearProgressBar();
    }

    /// <summary>
    /// 开始打包
    /// </summary>
    private void StartPack()
    {
        if (!Directory.Exists(OutPutPath))
        {
            Directory.CreateDirectory(OutPutPath);
        }
        BuildPipeline.BuildAssetBundles(OutPutPath, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.Refresh();
        Debug.Log("BuildAssetBundles Complete");
    }
}