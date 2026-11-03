# 已知限制

[English](../../en/reference/limitations.md) | [简体中文](limitations.md)

本页区分可见行为、原因和需要的后续集成。编译支持不等于在每种设备完成验证。

<a id="hybridinput-scroll-deltaonly"></a>
## HybridInput DeltaOnly 滚动

当前 Input System 提供者只承诺 DeltaOnly，无法识别手势 ID、gesture phase、momentum phase、
设备类型或滚动捕获。原有 uGUI 目标、冒泡和轴锁负责交付，不读取第二个原生 delta 来源。

### 回弹延迟

最后一个有效非零增量后，等待对象本地空闲延迟（默认 80 ms）才启动 Elastic 回弹，
所以手势结束后可能短暂停在拉伸位置。零 delta 回调可夹在后续非零回调之间，不能作为结束信号。
可调延迟、最大偏移和回弹时长，或关闭该对象 Elastic；这只是体验取舍，不能制造可靠结束信号。
立即准确释放需要真实 phase 且能确定关联到 uGUI 事件。

### 残余惯性阻挡正交手势

锁定轴 A 后，A 的惯性可能持续刷新超时，使新轴 B 手势延迟或被抑制，直到空闲重置。
没有手势身份时无法可靠区分新手势与旧手势漂移。按主导比例、累计距离或滞后重定向只是启发式取舍，
缩短 Elastic 释放延迟也不会结束被非零惯性维持的轴锁。
确定修复需要源手势 ID 或可靠关联的手势/惯性 phase；目前不靠单帧比例、时间接近或方向推测。

<a id="hybridinput-ime-composition-and-session-control"></a>
## IME 组合取消与会话控制

以下限制针对活动组合的严格取消和所有者转移，不表示不支持普通 IME 输入。
TouchScreenKeyboard 负责软键盘开闭及文本/选区同步，不拥有 Unity 的原生 UITextInput/InputConnection。

### macOS 候选窗口残留

切换时旧组合不会写入任何输入框，空格也不会冲刷它，新框仍可用，但系统候选窗口可能继续可见。
当前可经 `NSTextInputContext` 丢弃 marked text，却不拥有 Unity 的真实 `NSTextInputClient` 上下文。
旧托管 token 在取消前作废，残留是系统 UI，不是可继续写入的 Milestro 组合。
彻底关闭需要与 Unity 实际客户端上下文的已验证集成。

### iOS 严格取消未支持

需要严格取消时原生结果为 Unsupported，边界保持关闭直到 Unity 回报真实空组合确认。
尚未集成 Unity 的 UITextInput first responder，不能安全指令 UIKit 丢弃 marked text。
旧托管所有权先作废，普通 iOS IME 不因此被归类为不支持。
需要真实 first responder 接缝或具备等价所有权边界的移动文本会话模型。

### Android 严格取消未支持

没有 Unity served View 与活动 InputConnection 的桥接，不能保证所有权改变时主动丢弃 pending marked text。
当前路径不发原生取消请求（IsRequired=false），托管 session generation 拒绝旧回调，
其余平台组合完成/取消仍由 Unity 与 Android 决定。
需要已验证的 served editor 桥接或等价无跨所有者写入的模型。

## 功能范围

项目仍是概念验证，API 与组件面可能变化。托管和原生插件必须从同一修订一起部署。
MSAA 目标不支持，GLES 不接受 BGRA32；桌面 OpenGL/Vulkan 为实验选项。
详见 [渲染](rendering.md)。

竖排仅用于 Canvas TextBox，要求固定高度；不支持 vertical-lr、RTL、TextInput 编辑、SlimText
和世界空间竖排。Ruby 为单层注音；img 不自动绘图。详见 [文本](../guides/text.md) 与 [Ruby](ruby-layout.md)。

TextInput 和下拉框的专用手柄适配尚未实现或设备验证，不能以其他 uGUI 控件的导航推断支持。
下拉框不支持 World Space Canvas，移动 safe area、触摸与性能待真机验证。
屏幕阅读器结构化光标、选区和辅助技术直接修改文字受 Unity 模块限制，见 [无障碍设计](accessibility-design.md)。
