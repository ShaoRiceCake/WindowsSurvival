# 方案预览复核

2026-09-24，独立无界面 Chromium，原游戏像素字体加载完成后截图。

结果：`review.js` 39 项检查全部通过，捕获的浏览器运行时异常为 0。

- WebGL 霜冻 shader 成功编译、链接并输出透明效果层。
- 原游戏背景、卡图、字体加载完成；按 1920×1080 游戏坐标显示和检查。浏览器截取边界的像素取整使部分导出 PNG 多出 1 行，不影响布局比较。
- A/B/C × 舱室/水域/通道，共9种组合检查窗口和操作边界。
- 舱室不显示牵引绳，水域不显示隔热棉。
- 除冰按钮可见；冰封150→100仍封锁，100→50开放。
- 隔热棉拖入安装与拆除、浮标填盐/暖绒及耐久满时禁用生效。
- 粒子画面随时间变化；暂停后停止变化；改装窗口可覆盖季节效果层。
- 预览页面1280/760/430/375/320宽度无横向溢出。完整桌面游戏画面保持固定比例，窄屏可用原尺寸模式查看；此检查不表示游戏已适配手机版。
- 人工查看了窗口、通道、冰封四阶段及季节的浏览器渲染图；处理了 B 版状态标签与槽位的遮挡，并将 A/B 通道除冰按钮移至固定操作区。

本次只在 `Docs/ClimateUIConcepts/` 创建方案与源码。未对当前正在打开的 Unity 实例执行场景、Prefab、材质或 Play Mode 操作。Unity shader 未接入、未编译验收，性能需在选定方向后于游戏内验证。

素材来源：

- 底图：`Docs/ClimateValidation/temperature-windows.png`。
- 废金属占位图：由 `export-source-assets.py` 按 `Assets/Resources/Sprites/Resource.psd.meta` 的 Resource_1 原生切片导出，RGBA 原样保留；不含槽位背景。
- 气舱门、水域地点图：由 `Docs/ClimateValidation/passage-modifications.png` 和 `water-modifications.png` 在浏览器内按原区域展示。
- 舱室图：`Docs/SaveUIConcepts/motion/assets/动力舱.png`。
- 字体：`Assets/Art/Font/tianwangxingxiangsu.ttf`。

预览素材位于本目录的 `assets/`，截图位于 `previews/`。`index.html` 和 `overview.html` 可直接从本地打开；本机 HTTP 预览服务只是打开方案的便利入口。

## 色块断层修正

用户指出季节标签周围的硬边色块及卡图的蓝灰方底。原来的39项检查仅覆盖功能与布局，没有覆盖这两处材质合成问题。

已移除 WebGL 中按按钮矩形切割效果的逻辑，Unity shader 草案也取消相应遮罩入口。底栏统一底色，霜冻与渐变连续覆盖，季节/日期文字以透明背景置于上层。新卡占位图改为原始透明 Sprite，卡内空白区域统一使用本体底色。

本轮按用户指出的部位另外导出并人工检查 `dock-after.png` 与 `card-after.png`，同时刷新完整方案截图。
