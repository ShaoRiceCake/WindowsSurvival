# 存档功能改动核查 · 2026-09-18

后续更新：[编辑器进入流程修复](EditorEntry/README.md)补充了真实 Editor Play Mode 入口验证，并从 StartScene 删除旧界面。下文是鼠标修复当时的审计快照，其中“未修改场景”、文件数量与测试数量不代表后续更新后的状态。

核查对象为当前仓库的 Git 工作区（GitHub Desktop 的 Changes 使用同一份数据），包括已跟踪文件的差异和全部未跟踪文件；没有操作 GitHub Desktop 窗口，没有提交或推送。

## 鼠标问题与此前测试的缺口

旧实现中，`MouseManager.Update → SetCursor` 每帧把 `Cursor.visible` 设为 false，`SaveHubUI.LateUpdate` 又在窗口打开时设为 true。两个写入者相互覆盖，同时没有隐藏游戏自身的 Image 鼠标。主菜单和游戏场景都包含 MouseManager，因此两处打开存档窗口均受影响。

此前的 25 项 Development Player 检查验证了保存/读取、死亡、分享及直接调用按钮事件等路径，截图来自真实 Canvas 的离屏渲染；没有检查操作系统鼠标，也没有覆盖一帧内 Update/LateUpdate 的可见性冲突。此前报告不足以证明鼠标交互没有问题。

修复将系统鼠标与自定义鼠标的显示集中到 MouseManager，只在窗口打开、关闭、销毁、焦点恢复和场景鼠标初始化/销毁时更新。窗口打开时隐藏自定义鼠标及等待图像，关闭后恢复原有等待状态；删除了两处逐帧争抢写入。未修改鼠标贴图、位置换算或拖拽规则。

回归测试每个场景连续采样 30 帧，在 Update 与 LateUpdate 两个阶段检查系统鼠标、自定义鼠标和等待图像。覆盖主菜单、主菜单弹窗、进入游戏、三次开关游戏菜单、等待中打开窗口、等待结束、回档换场景及返回主菜单。旧版本的主菜单弹窗检查确实失败，证据见 `CursorRegression/before-checks.txt` 和 `before-details.txt`。首条失败采样为系统鼠标已显示但自定义图像仍显示；源代码同时确认了另一阶段的反向写入。

这些检查验证运行时显示状态，不等于人工观察物理鼠标移动，也不包含硬件光标录像。

最终结果：Windows Development Build 成功、0 个构建错误；40 项播放器检查与 521 条数据断言通过；13 组鼠标场景共 390 帧、780 次阶段采样，0 次显示状态不匹配。运行时无 Error/Exception，源码与验证副本的 28 份相关脚本逐字节一致。详见 `CursorRegression/summary.txt`、`checks.txt`、`cursor-checks.txt`、`data-tests.txt`。

## 既有脚本：14 个文件为何修改

以下路径均相对于 `Assets/Scripts/`。

| 文件 | 改动用途 | 审核结论 |
|---|---|---|
| `Chat/AfterChatFactory.cs` | 剧情死亡进入普通/硬核死亡流程 | 保留；修正了过时的“延迟删除”注释 |
| `Chat/ChatManager.cs` | 新世界线的教程选项、全局对话速度、等待对话完成再保存、恢复最后一条消息 | 保留，关系到教程回档与设置生效 |
| `Data/GameDataManager.cs` | 原数据分区接入快照捕获/加载，全局音量不参与回档 | 保留，为新旧存档系统的连接处 |
| `GameEvent/GameEventManager.cs` | 回档重置趋势累计值 | 保留，防止旧时间线状态残留 |
| `Main.cs` | 管理器初始化前加载待进入的快照，应用显示设置 | 保留 |
| `Manager/MouseManager.cs` | 保存时检测拖拽/等待状态；本轮统一鼠标显示控制 | 保留；修复本次引入的窗口冲突 |
| `Manager/SoundManager.cs` | 全局音量、音效和循环音源响应设置，避免总音量重复相乘 | 保留，为声音设置实际生效所需 |
| `Manager/StartSceneManager.cs` | 替换固定四档入口，接入世界线列表和设置 | 保留；恢复误删的编辑器退出 Play Mode 行为 |
| `Manager/TimeManager.cs` | 时间推进过程避免保存半完成状态，回档清理旧队列 | 保留 |
| `State/StateManager.cs` | 回档重置死亡标记，死亡进入新的存档规则 | 保留 |
| `UI/Controls/HoverableButton.cs` | 新弹窗暂停时使用不受时间缩放影响的动画；无音频管理器时不报错 | 保留；`useUnscaledTime` 默认 false，旧按钮沿用原行为 |
| `UI/Screen/Windows/WindowsManager.cs` | 原保存和退出按钮接入新流程，按结果显示保存反馈 | 保留 |
| `Utils/JsonManager.cs` | 快照捕获/读取、原子写入、导入时允许的类型与图数据转换 | 保留；导入不能直接信任任意多态类型 |
| `Utils/MySceneManager.cs` | 换场景停止旧协程、解除 UI 暂停、重置保存就绪状态 | 保留；PublicMono 跨场景，旧时间推进/资源请求/卡槽协程不可继续操作新时间线 |

没有改动卡牌定义、配方、地图、游戏窗口素材、现有场景或原有鼠标 Prefab。上述 7 个文件中无意义的 BOM 编码差异已恢复，避免把编码改动混入功能差异。

## 新增的正式功能与工具

| 文件/位置 | 用途 |
|---|---|
| `Assets/Scripts/Data/SaveModels.cs` | 世界线、保存点、创建选项、麦麦随机命名、实际游玩计时 |
| `Assets/Scripts/Data/SaveRepository.cs` | 保存点仓库、自动轮换、复制分享、校验与旧档兼容 |
| `Assets/Scripts/Data/SaveDataContract.cs` | 完整快照分区校验与初始数据 |
| `Assets/Scripts/Data/SaveSystem.cs` | 游戏保存/读取、自动保存、退出、死亡与场景衔接 |
| `Assets/Scripts/Data/GameSettings.cs` | 独立于世界线的全局设置持久化 |
| `Assets/Scripts/UI/Screen/SaveHubUI.cs` | 世界线、历史记录、创建、设置与确认窗口的数据及操作 |
| `Assets/Scripts/UI/Screen/SaveHubButton.cs` | 单层边框颜色、按下和键盘选择反馈 |
| `Assets/Scripts/UI/Screen/SaveJournalRow.cs` | B 版时间轴记录的展开/收起 |
| `Assets/Scripts/UI/Screen/SaveSettingRegistry.cs` | 可拓展设置项注册 |
| `Assets/Scripts/UI/Screen/SaveWindowDrag.cs` | 新窗口拖动 |
| `Assets/Scripts/UI/Screen/SaveFileDialog.cs` | 导入世界线的本地文件选择 |
| `Assets/Resources/Prefabs/UI/SaveHub.prefab` | 使用已有 UI 素材的正式序列化界面 |
| `Assets/Editor/SaveHubBuilder.cs` | 手动重建正式 Prefab 的工具，保留 |
| `Assets/Editor/SaveFeatureValidation.cs` | 独立数据验证及测试构建入口，仅编辑器编译 |
| `Assets/Scripts/Data/SaveRuntimeValidation.cs` | 回归测试，仅编辑器/Development Build 编译且须命令行显式启用；普通发行版不包含 |

每份 Unity 资源/脚本的 `.meta` 用于保持 GUID 引用，属于必要文件。

## 已清理的不必要内容

1. `Assets/Design/SaveUIConcepts` 的 12 份静态方案 Prefab，以及目录/资源 meta。
2. `Assets/Editor/SaveUIConcepts.cs`、`SaveInteractionAssets.cs` 两份一次性导出工具及 meta。
   以上共 30 个文件归档到 `Docs/SaveUIConcepts/authoring-archive.zip`，逐文件校验压缩包内容后才移出 Assets；原文件另保存在本机忽略的 Temp 目录，可恢复。设计网页与图片保持可用。
3. `.playwright-cli/` 与 `output/playwright/` 共 26 个浏览器中间产物加入精确忽略规则。文件仍在本机，不再混入提交候选；正式证据在 Docs 中。
4. 删除 SaveHubBuilder 的编辑器常驻文件轮询，以及加载编辑器时自动重建界面的逻辑。工具保留显式菜单/命令入口。
5. 修订 `Docs/SAVE_SYSTEM.md` 中仍描述“退出保存永久保留”“按钮悬停反色”的过期说明。

清理前为 162 个文件条目。清理中间产物后为 108 个：19 个已跟踪改动（包含 `.gitignore` 和下述 4 个既有无关文件）、30 个新增 Unity 文件（包含 meta）、59 个文档/设计/历史验证文件。本次审计与鼠标回归证据会增加少量 Docs 文件；最终数量以 `CursorRegression/summary.txt` 为准。

## 不属于本次功能、保持原样的既有改动

- `Assets/Resources/ScriptableObject/Craft/Recipes/钢铲.asset`：配方资产差异。
- `ProjectSettings/ProjectSettings.asset`：Android 图标配置条目，存档系统不需要修改它。
- `Assets/AddressableAssetsData/link.xml` 及 `.meta`：原有删除状态，存档系统不需要删除它们。

这些文件不应因清理存档功能而盲目回退。全仓 `git diff --check` 仍会指出既有 Android 图标配置里的行尾空格；本次修改的脚本与 `.gitignore` 单独检查。
