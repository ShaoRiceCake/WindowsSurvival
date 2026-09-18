# 存档与设置验证记录

**最新验证：** [Unity 编辑器进入流程、CanvasGroup 修复及旧界面清理](EditorEntry/README.md)。此前核查：[鼠标修复与改动审计](CHANGE_AUDIT.md)、[鼠标回归结果](CursorRegression/summary.txt)。界面基线为 [B1 实装与退出保存合并验证](B1/README.md)。下方为此前迭代记录，其中“退出记录永久保留”和“悬停反色”已经被新规则替换。

验证在独立 Unity 工程副本和隔离存档目录中完成，没有使用玩家的真实存档进行破坏性测试。

## 已验证

- 数据层 518 条断言：自动轮换、手动/退出记录保留、独立复制与分享导入、硬核最新进度与死亡失效、损坏/缺失数据拒绝、非法反序列化类型拒绝、10 分钟计时边界、暂停/后台排除、提交失败保留旧索引，以及 500 个麦麦世界线名。
- Unity Play Mode：创建、手动保存、保存点详情、读取、退出保存、普通死亡界面。
- Windows Development Player：实际健康值修改后回档恢复；真实游戏快照分享往返；硬核只留最新、复制转普通和死亡删除；教程对话保存与恢复；全局主音量和音效通道增益。
- 窗口重做：使用现有 CustomWindow / TopBar / CustomWindowButton / ScrollView 预制体；已做新旧窗口同屏对照。暂停状态下按钮悬停/退出、名称随机按钮、标题栏拖动均通过播放器回归。存档详情不显示角色状态。
- 实际播放器截图：1920×1080 创建页及历史页；1280×720 设置页。名称旁的随机按钮、较长双卡牌世界线名、类型筛选和详情排版均已检查。

## 按钮边框修订

操作按钮、随机按钮、分类筛选及列表项均增加现有 UI_Panel 素材的常驻边框，保留原有悬停反色与绿色选中框。最新 Play Mode 截图为 `history-accent.png`、`settings-accent.png`、`creation-buttons.png`；`button-hover.png` 和 `button-click.png` 记录随机按钮悬停和点击后的表现。已检查文字与边框无重叠。早期播放器截图作为上轮验证记录保留。

## 证据

- `data-tests.txt`：数据与命名断言。
- `play-mode.txt`：Play Mode 流程结果。
- `checks.txt`：Windows 播放器检查结果。
- `build.txt`：Windows 构建结果。
- `creation.png`、`history.png`、`settings-720p.png`、`window-comparison.png`：实际播放器截图。
- `settings-accent.png`、`history-accent.png`：最终点缀色版本的 Play Mode 渲染截图。点缀色为用户提供图片中的 RGB(100, 222, 174)，只用于选中项细框。

## 边界

- 1280×720 的新增设置面板没有发现重叠或截断；现有游戏 Pixel Perfect Camera 在低于其参考分辨率时仍显示开发版警告。本次没有改造原有游戏画面的低分辨率适配。
- 原生 Windows 文件选择框的人手点击，以及显示模式的 15 秒确认/超时恢复，没有做人工交互验收。分享文件生成和导入数据路径已自动验证。
- 未对真实玩家旧档执行迁移验证；代码保留旧文件，并拒绝缺少必要分区的旧档。
- 尚未逐项覆盖所有后期设备、事件、加工中间状态与完整剧情分支。沿用原项目的数据采集，新增安全时机等待和完整快照提交。
