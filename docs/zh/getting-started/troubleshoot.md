# 排查空白、缺失与输入问题

[English](../../en/getting-started/troubleshoot.md) | [简体中文](troubleshoot.md)

先按症状找到最小检查项，再调整样式。

## 完全没有文字

1. 确认对象上有启用的 `TextBox`，且位于启用的 Canvas 下。
2. 确认 `RectTransform` 宽高非零，没有被父级遮罩裁掉。
3. 确认 `TextBoxRenderTextureProducer.content` 非空。
4. 先处理 Console 中的 `DllNotFoundException` 和 ICU 错误。

默认文字为白色；浅色背景可设置 `producer.textStyle.textColor = Color.black`。

## DllNotFoundException: libMilestro

托管代码已导入，但当前平台/架构的原生库未加载。
检查 `Assets/` 下的 `Milestro/Plugins/<platform>/<library>`，默认位于
`Assets/unity-plugins/Milestro/Plugins/`。预编译包仅包含安装页列出的架构。
检查插件导入设置与目标平台，且托管与原生文件来自同一构建。
iOS 导出的 Xcode 项目需要包含 `FrameworkBinding.cpp` 和 Milestro framework。

## ICU 初始化失败

确认存在 `Resources/Milestro/icudtl.dat.bytes`。默认完整路径是
`Assets/unity-plugins/Resources/Milestro/icudtl.dat.bytes`，Resources 可位于 Assets 下任意深度。
源码方式可从 `ext/icu-cmake/common/icudtl.dat` 生成，或运行 `./gradlew packageUnityPlugin`。
Unity 通过 `.bytes` 将其作为 `TextAsset` 导入，加载键是 `Milestro/icudtl.dat`。
自行初始化时才定义 `MILESTRO_NO_ICU_INIT`，见 [运行时配置](../reference/configuration.md)。

## TextInput 无法聚焦或打字

依次确认：只有一个活动 `EventSystem`；使用 `InputSystemUIInputModule`；Input System
版本在 `[1.16.0,2.0.0-0)`；Active Input Handling 为 New 或 Both。
检查 `HybridInputRuntime.Diagnostics` 的 `SelectionStatus`、`ProviderId`、`LastDiagnostic`。
旧 `StandaloneInputModule` 可做普通 UI 导航，但无法让 `TextInput` 获得严格焦点会话。
详见 [HybridInput](../reference/hybrid-input.md)。

## 聚焦正常，但 IME 行为不同

普通组合与已提交文本可用；活动组合的严格取消和跨输入框所有权转移受平台限制。
macOS 切换后可能残留候选窗口，iOS/Android 无法严格取消宿主编辑器的组合。
见 [平台限制](../reference/limitations.md#hybridinput-ime-composition-and-session-control)。

## 标签原样显示，或出现 Render Error

XML 标签原样显示时，在 producer 的 **Rich Text Parser** 中选择 **Milestro Rich Text**，
或设置 `producer.richTextParserId = Milestro.RichTextParser.XmlRichTextParser.Id`，默认是纯文本。
Markdown 内容选择 **Markdown**（`markdown`），需安装模块并注册 provider。
未知解析器 id 会报错，不会回退到纯文本。
启用后按 XML 规则转义 `&`，检查未知标签、嵌套 `<a>`、多个 `<rt>`、空属性值和 XML 格式。
Editor 会显示详细解析错误，Player 回退为 `Render Error`，不自动退回纯文本。
参阅 [富文本语法](../guides/text.md#rich-text)。

## 被裁剪、字号变小或不换行

逐项检查 `wrapMode`、`paragraphStyle.singleLine`、`textOverflow`、边距、
`TextBoxLayoutElement` 的尺寸模式和 `enableAutoFontSize`。
见 [布局指南](../guides/layout.md) 与 [参数参考](../reference/layout-engine.md)。

## 缺少 ParaParty 程序集

按 [安装](install.md) 检查两个 Git 依赖。Package Manager 解析需要 Git 和网络，
等待解析完成再运行。

## Editor 正常，Player 失败

检查目标架构原生插件是否被打包、当前图形 API 是否有对应后端、托管和原生版本是否一致，
以及渲染目标是否使用了不支持的 MSAA/格式。详见 [渲染参考](../reference/rendering.md)。
