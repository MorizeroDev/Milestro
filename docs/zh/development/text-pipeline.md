# 文字管线实现

[English](../../en/development/text-pipeline.md) | [简体中文](text-pipeline.md)

本页记录文字准备、缓存和字形输出的实现。组件配置及回退条件见
[渲染参考](../reference/rendering.md#experimental-glyph-meshes)。

## 更新调度与布局

`TextPreparationScheduler` 收集 Canvas producer 的修改，并在 uGUI `PostLayout` 统一处理。
连续 setter 共享一次待处理工作；显式刷新会消费待处理更新，处理过程中产生的新修改则留待后续执行。
禁用或销毁 producer 时会取消其待处理工作。世界空间和独立 producer 保留同步更新路径。

SlimText 保存原生字形位置和度量，让测量与纹理绘制复用同一布局。
供图集使用的紧凑视图带有所有者版本，必须同步消费；已发布的网格输出独立持有缓冲区。
Paragraph 则保存从实际绘制中导出的独立快照，导出不会额外触发 shaping 或 Layout。

动态 Paragraph 仍需构建和布局。NoWrap 的自然宽度探测与最终布局输入不同，两次 Layout 都保留。
宽度查询使用 UTF-16 范围，原生 UTF-8 输入按与编辑一致的 SkUnicode 规则计数。
修改这里时，应保留补充平面字符、非法输入、行尾空白和占位符的语义。

基础文字样式投影只复制富文本路径原先保留的字段，不是完整 clone。
随后仍按原顺序应用 block 和 payload 的覆盖，包括缩进、省略和竖排设置。

## 字体配置与缓存失效

托管同步每次读取实际字体配置。空配置清除用户映射；非空配置通过
`MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings` 传入完整表，
由原生代码与当前生效表比较后再决定是否替换。

归一化后的关键字插入顺序不影响等值判断，但 token 类型、值和排列顺序都参与比较。
空覆盖与缺少映射也不同。等值表保留缓存；实际变化、单个原生 setter、显式清缓存和成功注册字体
都会使缓存失效。对已经为空的表执行 reset 也会保留缓存。

生效表由原生集合持有，因此直接原生修改和集合重建会在下一次托管同步时被重新应用的配置覆盖，
不需要托管镜像。现有可变字典和返回列表不支持与同步并发修改。
配置不变时仍有跨界传输和比较成本。

缓存保留是 CPU 与内存的取舍。Paragraph 缓存限制条目数，但 FontCollection 的 face、variation
缓存没有明确容量。条目上限也不等于字节上限，Unity 和托管内存计数不能覆盖完整原生缓存。

## 字形网格与纹理呈现

`TextGlyphAtlas` 为受支持的 CanvasSlimText 和 TextBox 输出共享 Alpha8 页面。
Paragraph 的 shaping、字体回退和断行仍由 SkParagraph 负责。
字形键包含字体身份、栅格设置、倍率和量化后的子像素相位；根据字体标志，
相位可采用四分之一像素步进，并保留 baseline snapping。
因此，每个字符出现过一次并不代表所有字形与相位组合都已预热。

页面在被引用时保持 UV 稳定。单个输出必须位于一页，仍有输出引用的页面不会回收复用。
网格缓冲区会尽量复用，但动态布局和 Canvas 更新不保证零分配。

默认 Canvas 纹理呈现使用 `Resources/Milestro/TextPremultiplied.shader`，
避免边缘覆盖率被重复相乘。这可能改变旧版本的文字边缘观感，自定义材质仍按自身逻辑处理。
字形网格使用 `Resources/Milestro/TextGlyphMesh.shader`。

## 验证状态

受控短文本对照已证明 CPU 帧成本下降，但小负载纹理案例超出完成率回退预算，
所以整体性能验收仍未通过。最终收敛和采样的 CPU 延迟通过，完成率差异原因与长期原生内存上界
尚未确认。这些结果不能用于宣称 GPU 或实际屏幕显示收益。

进一步优化已暂停，字形网格继续默认关闭。[验证指南](text-pipeline-validation.md)介绍保留的工具；
外部焦点观察器和分析脚本没有纳入仓库。
