# Unity 编辑器进入流程修复 · 2026-09-18

## 原因与修复

用户截图中的 `MissingComponentException` 来自 SaveWindow 缺少 CanvasGroup，与旧存档数据无关。此前使用 `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()`；Unity 编辑器中的缺失组件包装对象并不按普通 C# null 参与 `??` 判断，导致补组件分支没有执行。发行播放器检查未覆盖这个编辑器差异。

- 正式 `SaveHub.prefab` 的窗口与详情区域均已序列化 CanvasGroup。
- 运行时兼容处理改为 `TryGetComponent`，窗口打开、关闭及页面渐变统一使用；窗口未打开时忽略关闭操作。
- 生成工具也保存上述组件。验证和构建入口不再自动重建 Prefab，直接验证项目提供的正式资源，避免测试阶段掩盖资源缺漏。
- 从 `StartScene.unity` 真正删除 LoadButton（旧四档列表）和 ChooseSkipGuide（旧教程选项）两组层级，共 37 个 GameObject、164 个序列化对象；删除旧字段引用。逐 fileID 对比，存活对象仅修改 Canvas 子对象列表和 StartSceneManager 的两个旧引用；插画、标题、相机、现有按钮、鼠标等对象内容未变。

## 旧存档清除

已从 `C:/Users/22575/AppData/LocalLow/DefaultCompany/WindowSurvival` 移走旧 LoadData、空的 GameData0，以及带 legacySlot 标记的迁移世界线。当前保留 1 个新版世界线及全局设置。

备份：`C:/Users/22575/AppData/Local/Temp/WindowsSurvival-LegacyBackup-20260918-154853`。备份中 4 个文件逐文件 SHA-256 校验通过，`cleanup.json` 记录精确路径。旧数据不再位于游戏读取目录，也不会再次触发迁移。

## 本轮确实执行的引擎验证

引擎：Unity 2022.3.34f1c1 **Editor Play Mode**，在隔离工程副本中执行，未控制或关闭用户现有 Unity 编辑器。

场景、Prefab 和本轮修改的脚本与正式工作区逐字节一致，校验值见 `asset-verification.txt`。测试使用隔离数据目录；没有重新生成测试专用的 Prefab。

通过 EventSystem 的射线命中检查后发送 PointerClick，覆盖：主菜单「进入游戏」→新窗口关闭→主菜单「设置」→重新进入→创建世界线→切换教程→「开始游戏」→手动保存→界面选择读取及确认→普通死亡→返回主菜单→再次打开世界线列表。同时检查包括 inactive 对象在内不存在旧界面节点，并捕获任何 Unity Error、Exception、Assert 为测试失败。

- `play.txt`：上述完整流程通过。
- `data-tests.txt`：523 条断言通过，新增正式 Prefab 的两个 CanvasGroup 检查。
- `runs.png`、`creation.png`、`return-runs.png`：本轮 Editor Play Mode 实际 Canvas 渲染，已查看。

图片是引擎内 Canvas 离屏渲染，不是操作系统鼠标录像。自动化发送真实 UI 事件，不代表人工操纵物理鼠标。当前已打开的场景若仍是编辑器内的旧副本，退出 Play Mode 后重新打开 `Assets/Scenes/StartScene.unity`，加载已删除旧层级的正式场景。
