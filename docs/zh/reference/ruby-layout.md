# Ruby 注音排版

[English](../../en/reference/ruby-layout.md) | [简体中文](ruby-layout.md)

Ruby 使用普通 `ParagraphBuilder::Build()`、`Paragraph::layout()`、`paint()` 管线，
并参与行度量、visitor、路径、原文范围与命中测试。无 Ruby 的段落沿用上游布局路径。

## 输入与索引

C++ 范围是半开 UTF-8 字节偏移：

```cpp
constexpr char base[] = "漢字後";
constexpr char annotation[] = "かんじ";
builder.addText(base, sizeof(base) - 1);
builder.addRuby(0, 6, annotation, sizeof(annotation) - 1);
auto paragraph = builder.Build();
paragraph->layout(200);
paragraph->paint(canvas, 0, 0);
```

C++ API 为 `addRuby(startUtf8, endUtf8, text, length)`，也接受 RubyStyle 重载；C ABI 还导出
`MilestroSkiaTextlayoutParagraphBuilderAddRubyWithStyle`。Unity 提供 `AddRubyUtf8`、`AddRubyUtf16`，
新增代码优先用 `AddRubyText(baseText, annotation, style)`，无需自行算偏移。
上例 `AddRubyUtf8(0, 6, "かんじ")` 标注前两个基文字。

Ruby 不插入基文；后续 UTF-16 API 仍针对原始基文索引。
非 ASCII 输入的字节与 UTF-16 偏移不同。

```csharp
var rubyStyle = new RubyStyle
{
    Position = RubyPosition.Above,
    Alignment = RubyAlignment.SpaceAround,
    FontSizeMultiplier = 0.5f,
    Gap = 2,
};
builder.AddRubyText("漢字", "かんじ", rubyStyle);
builder.AddText("後");
```

注音可按任意顺序注册但不能重叠。Build 检查范围，layout 检查 Unicode、塑形簇与双向合法性。
C ABI 验证抛错时返回 `MILESTRO_API_RET_FAILED`，AddRuby 成功不保证后续成功。
清空重新构建需创建新 builder；Milestro 各层不暴露 Skia Reset。

## 宽度与断行

- 每段注册范围是原子 Ruby 组，基文与注音同列；相邻独立组仍可分开换行。
- 默认宽度取基文与注音的推进/墨迹范围较大者。Allow 按基文宽度分配并允许注音溢出，不做相邻碰撞规避。
- 支持 Start、Center、End、SpaceAround、SpaceBetween；Start/End 跟随双向解析，分散模式向较短一侧加间隔。
- 注音单行置于基文上方/下方，继承首个基文样式或 RubyStyle.textStyle，再乘字号倍率；默认 0.5、间隙 2，实际字体度量影响行高。
- 基文 line-height 可改变行框，但不会改变注音大小或其与基文距离；注音忽略继承与显式 height 覆盖。
- ICU 软/硬断点优先；紧急换行仍保护字素簇、塑形簇、标点、NBSP、word joiner 与组合序列。不可断组超宽时溢出，不缩字号。
- 默认 PreserveBase 将超宽组整体移向容器可用区域，保留前方可视单元间距但不改变分配/固有宽度。
  零宽容器、基文自身过宽或周边不可断标点仍可能不合适；Inline 保留组内居中。
- Ellipsis 删除完整 Ruby 组，隐藏组不生成注音字形或基文选区框；Justify 在组间分配而不改组内基文间距。
- 硬换行保留空行和结尾空行；最大行数、尾随空格、高度及占位符沿用正常段落表示。

## 双向文字

基文字形顺序来自 ICU bidi 与 HarfBuzz。LTR/RTL、多脚本、数字、隔离/覆盖控制及换行 RTL 行保留原生视觉顺序。
注音在独立段落塑形，方向继承父段落。

范围必须处于同一解析 bidi level，不能跨 level 或包含方向控制；控制字符可包围范围。
范围还需对齐原始字素簇和塑形簇边界，不可包含占位符或硬换行。
注音须非空、单行、无 tab。

## 查询、缓存与竖排

visit/extendedVisit 在基文行包含注音字形，标记 `Paragraph::kRubyAnnotation_VisitorFlag`。
注音 glyph 映射回基文范围，不在源文本中获得独立位置；getPath 包含基文和注音轮廓。
普通范围、glyph、光标查询返回基文布局单元（含 Ruby 留白），不提供独立注音选区、编辑或墨迹框。
超宽组移动时，部分范围单元可能与当前可见墨迹不同；专用精确墨迹查询和注音命中测试仍未完成。

ParagraphCache 只共享未变化的基文塑形；Ruby 添加留白前分离 glyph 存储，并在宽度变化时恢复放置偏移。
不同注音与独立测量/绘制段落不修改彼此的缓存基文塑形。

vertical-rl 时注音放在列的 line-over 侧（物理右边）。`ruby-position: under` 降级到该侧并记录 Editor 诊断；
原子性、尺寸、断行及查询规则不变。

## 范围与验证

多层注音与独立测量 API 尚未提供。Unity 解析器支持单个基文 HTML 子集
`<ruby>base<rt>annotation</rt></ruby>`、可选 `<rp>`、标准 ruby-position/ruby-align/ruby-overhang，
以及 `--milestro-ruby-*` 属性。InputBox 编辑有意保持纯文本。
Ruby 范围不能交叠 tate-chu-yoko；校验会拒绝。即使 base 有 text-orientation 覆盖，注音始终使用 mixed。

常规 CMake 测试注册 MilestroTest_Ruby，并在 SkiaParagraphHitTest 覆盖导出 API：

```sh
cmake --build <build-dir>
ctest --test-dir <build-dir> --output-on-failure
```

CPU PPM 样本写入 `<build-dir>/tests/ruby/results/`。
