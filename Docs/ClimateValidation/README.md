# V6 温度与改装验收记录

日期：2026-09-24。Unity：2022.3.34f1c1。结果：150项Play Mode检查通过；运行期间无Error、Exception、Assert。完整逐项结果见 [tests.txt](tests.txt)。

测试通过 `SaveSystem.StorageRootOverride` 和 `GameSettings.StorageRootOverride` 使用独立临时目录，不读取或覆盖玩家世界线。旧世界线截图中的 `old` 是人工构造的测试记录，不是玩家存档。

## 覆盖范围

- 热交换、面积与隔热、昼夜、冰层季阶段和跨日天气保存；体温阈值及数值边界。
- 8种新卡牌运行时注册与废金属占位图、6张制作配方科技绑定、一次制作3个暖暖贴。
- 双向通道引用同一个浮标原对象；冻融、封锁、除冰、工具耐久、浮标填充与销毁。
- 地点改装条件显隐、拖入布置、浮标填充、隔热棉不可叠加及拆回原卡、牵引绳移动和耐久；牵引绳拒绝盐填充，材料不被消耗。
- 营火内容物槽接收盐水、熄火不加工、点燃后15分钟一换一得到盐、盐不会烤焦、自热烹饪袋不能制盐；海域无盐掉落，载入旧试行世界线清理错误来源但保留已持有盐卡。
- 载入已有盐水/盐卡会从卡牌表补齐缺失的加工配置，避免只让新生成的盐水能够制盐。
- 第10天未开始、第11天开始、第17天极寒、第34天结束与第35天温和季的日期边界；日历显示“冰层季”，尚未开始的旧默认排期修正。
- 保温服耐久不足时的保护溢出、暖暖贴体温上限、电缆损坏断开真实设备且禁止重连。
- 作物舒适/生长/存活温区、压力积累、成熟作物也会死亡；真实燃烧热源的坐标和温度影响。
- 真实保存后重新进入场景，浮标UUID、附件归属、冰封、温度和电缆耐久保持不变。
- 不兼容世界线可查看与删除、不可载入或复制；随后新建世界线正常。
- 改装行宽320/375/430时正文和操作区留空、4个除冰/移动按钮全部位于可视区；地点按钮文字实际可渲染，三种探索状态均能放下。

## 游戏相机截图

以下为真实Play Mode中的Canvas相机渲染，不是设计稿。改装内容超出窗口高度时沿用滚动列表；截图中的事件通知沿用项目原有提示系统。截图使用测试状态：例如舱室已安装隔热棉所以显示3级，而新世界的三舱初始均为2级。

| 内容 | 截图 |
| --- | --- |
| 地点与体温窗口、探索/改装入口 | [temperature-windows.png](temperature-windows.png) |
| 舱室电缆与隔热棉列表 | [cabin-modifications.png](cabin-modifications.png) |
| 通道浮标、冰封框与除冰操作 | [passage-modifications.png](passage-modifications.png) |
| 320宽改装行与适配后动作栏 | [passage-narrow-row.png](passage-narrow-row.png) |
| 水域牵引绳改装 | [water-modifications.png](water-modifications.png) |
| 1280×720分辨率 | [water-modifications-1280.png](water-modifications-1280.png) |
| 不兼容世界线提示与禁用载入 | [incompatible-worldline.png](incompatible-worldline.png) |

## 复现

关闭占用该项目的Unity实例后，以本机Unity执行：

```text
Unity.exe -batchmode -projectPath "C:\Users\22575\Documents\GitHub\WindowsSurvival" -executeMethod ClimateValidation.Run -logFile "Logs/ClimateValidation/play-salt-final.log"
```

不加 `-quit`，验证器会等待Play Mode和场景重载结束后自行退出。该入口会先运行资产构建器、按本次策划值重建目标配置与UI；后续手工调参后请勿直接运行，以免覆盖调参。源日志在 `Logs/ClimateValidation/`，本目录保存此次验收结果。

## 验证边界

本次完成Editor编译、真实Play Mode交互、保存/载入及UI像素复核；未进行独立播放器打包、全24天人工生存流程或长期经济平衡测试。首次冰层季第11天与营火制盐15分钟一换一已按用户确认配置；暖绒掉落数量仍为补充可调参数，见 [实装说明](../CLIMATE_V6_IMPLEMENTATION.md)。未执行Git提交或推送。
