# 设置 B 版实装与验证

2026-09-18，Unity 2022.3.34f1c1。使用隔离工程与独立测试存档目录。

## 实装方式

- `SaveHub.prefab` 嵌套 `Settings/SaveSettingsPanel.prefab`。侧栏、游戏页、保存摘要、主要/次要动作及设置页框架是序列化节点，可直接在 Prefab Mode 编辑。
- `Settings/SaveSettingRow.prefab` 保存开关、按钮、步进器和音量滑块的布局与引用。
- 复用 `CustomWindow` / `TopBar`、`CustomWindowButton`、`UI_HoveredFrame`、现有 `ScrollView` 和水平滚动条、像素字体、`Icons_Settings`、地点图、`ModalBackdrop`。
- `SaveSettingsView` / `SaveSettingRow` 仅绑定数据、实例化注册条目的 Prefab 模板、播放页面淡入。没有运行时 `new GameObject` 拼装布局或 IMGUI 绘制。
- 设置窗口改为 1290 × 720，固定左分类与底部按钮；内容过多时滚动。新增分类、条目继续使用 `SaveSettingRegistry.Register`。
- 继续游戏使用主要描边，返回主菜单与退出缩小并降低明度。悬停只改变原边框；按下文字下沉 1 px；箭头短距离位移。
- 保存、读取、返回主菜单、退出沿用现有功能。读取先进入当前世界线的记录页；保存成功刷新摘要。错误使用短时提示，避免设置页隐藏通用状态栏后丢失反馈。

生成工具 `SaveHubBuilder` / `SaveSettingsBuilder` 只在手动调用时生成并保存资源，不在游戏启动或编辑器加载时自动重建。普通布局维护直接编辑 Prefab 即可；手动重建会覆盖手工布局修改。

## 验证结果

- Editor Play Mode：设置入口、创建、手动保存、回档、返回主菜单、世界线列表、长名称、复制/删除、滚动与硬核记录规则通过。无 Unity Error/Exception/Assert；没有在测试入口中重建 Prefab。
- Editor 显示页：编辑保持草稿、应用打开确认、取消回退、15 秒超时回退均通过。
- Windows Development Player：76 条检查全部通过，没有 errors.txt 或 timeout.txt，执行保存并退出后到达应用退出回调。
- B 版专项覆盖：游戏底栏真实射线点击；旧设置区域隐藏；窗口比例与按钮层级；名称标签与长名称；手动保存摘要刷新；读取先打开记录页；音量滑块修改实际音量；自动保存关闭时禁用间隔；临时注册分类和 12 条设置的滚动扩展；取消退出恢复游戏页；原休息遮罩阻挡背景射线。
- 光标 13 组、780 次采样，全部无差异。
- 最终构建 `Succeeded errors=0`。15 个相关脚本、场景与 Prefab 的工作区/测试工程 SHA-256 一致，见 `source-hashes.txt`。

截图来自运行中的 Unity Canvas，通过相机输出。截图工具在临时切换 Overlay → Camera 后主动刷新图形缓存，以消除导出中漏掉边框的情况；没有修改游戏呈现逻辑或加工截图。`render-checks.txt` 记录 8 次选中/主要按钮的边框像素与剔除状态检查。

## 本轮范围

本轮修改集中于设置布局、模板、绑定与验证。没有修改 GameScene / StartScene、鼠标管理、存档格式、卡牌配方或其他窗口素材；这些文件已有的工作区改动继续保留。

仓库整体 `git diff --check` 仍报告此前 Unity 场景/ProjectSettings 的空字段尾随空格；本轮没有改动这些序列化行。
