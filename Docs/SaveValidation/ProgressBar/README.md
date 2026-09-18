# 保存 / 加载进度条检查

2026-09-18，Unity 2022.3.34f1c1，隔离工程 Editor Play Mode。

- 保存和载入共用 `SaveTransitionUI`；移除默认圆角渐变贴图，使用左端固定的实心矩形。
- 分别向真实 Prefab 组件传入 0%、25%、50%、75%、100% 进度，检查宽度与左端位置，并读取 Unity 相机渲染的像素，确认填充区无渐变、剩余区为灰色。
- 检查再次打开归零、完成时填满、0.2 秒后仍可见且阻挡输入、至少显示 0.8 秒、结束后释放输入。共 26 项通过，无 Unity Error / Exception / Assert。
- `save-50.png`、`load-75.png` 为引擎截图；为便于查看，仅截取过渡窗口区域。
- 此次验证聚焦进度条渲染和过渡组件，未重新执行完整的存档读写回归。未访问玩家实际存档。

游戏改动仅涉及 `SaveTransitionUI.cs`、`SaveHubBuilder.cs` 和 `SaveTransition.prefab`。保留窗口、字体、颜色、最短显示时长及现有存档流程。
