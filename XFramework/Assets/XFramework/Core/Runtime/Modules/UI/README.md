**GUIFramework使用说明**

 1. UIName类  定义一堆面板名;
 2. UIPath文件 为自定义格式文本，由UIMgr解析成面板资源路径，由面板名和预制体路径组成，面板名要和UIName保持一致;
 3. 所有面板脚本继承BasePanel,子类名称定义为UIName;
 4. UI组件的获取不允许使用transform.Find(),例如按钮，使用(this["BtnName"] as GUButton).button
 5. UI面板预制体放在Resources/UIPanelPrefabs/.../UIName.prefab，路径在UIPath中配置，只需要配置...中的部分
 6. UIMgr只负责管理与面板显示隐藏相关的逻辑，其余的一些工具类方法写在UIHelper中，UIMgr的逻辑控制也通过UIHelper调用