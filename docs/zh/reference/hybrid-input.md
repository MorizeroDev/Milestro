# HybridInput 提供者契约

[English](../../en/reference/hybrid-input.md) | [简体中文](hybrid-input.md)

进程级分发器统一处理按键、已提交文字、IME 组合和所有权。
TextInput 注册私有帧接收端，只有焦点所有者接收文本。提供者、设备、应用焦点或组件生命周期重置时清除挂起状态。

## 提供者选择

自动选择依据唯一活动 EventSystem 的当前模块：

| 模块 | Provider ID | 可用条件 |
| --- | --- | --- |
| StandaloneInputModule | legacy | ENABLE_LEGACY_INPUT_MANAGER |
| InputSystemUIInputModule | input-system | Input System 为 [1.16.0,2.0.0-0) |
| InputSystemUIInputModule | legacy DeltaOnly 回退 | Both 且包不在支持范围 |

无匹配、多个活动 EventSystem 或同等级冲突时拒绝选择。
检查 HybridInputRuntime.Diagnostics：SelectionStatus 区分 selected/no-match/conflict/missing-override/rejected-override；
ProviderId、ProviderKind、Capabilities、ScrollCapability、ActiveModuleType、EventSystemCount 描述环境。
ImeCancellationResult 与 ImeCancellationFailureCount 记录取消结果及非 Succeeded 次数。
InputSystemPackageStatus 独立区分 NotApplicable、Missing、BelowMinimum、Supported、Unsupported，
不改变选择状态的含义。LastDiagnostic、DiagnosticCount 记录会话隔离、环溢出、耗尽和监听器异常。

SetProviderOverride("legacy") / SetProviderOverride("input-system") 请求指定提供者，传 null 恢复自动。
覆盖不能绕过环境匹配。自定义 IHybridInputProvider 通过 RegisterProvider 注册，Dispose 返回的注册对象可移除。

## 严格焦点会话

发布聚焦按键、文字或组合的提供者必须实现 IHybridInputFocusSessionProvider。
每次 BeginFocusSession 获得新的不可变 IHybridInputEventSink；绑定源回调时捕获该 sink，
不能在回调执行时读取可变的“当前 sink”。此契约要求主线程回调。

旧 sink 在冲刷已接受的尾部事件前先封闭；之后调用在修改序号、环和按键状态前丢弃。
提供者切换、分发器重置、sink 释放和设备会话替换同样作废旧 sink。
Input System 适配器每个会话重新绑定文字、组合、按键边沿闭包；边沿在源 after-update 捕获，
Collect 仅快照 held state，不重新归属旧边沿。

legacy 和 Both 回退可参与选择或自定义用途，但 Input.inputString 轮询无法提供采集时所有权，
目前不实现严格会话，不能让 TextInput 聚焦。拒绝时报告 SessionIsolationUnsupported，没有尽力式非隔离回退。

## 有界分发

焦点意图、输入和生命周期通知在非递归事务泵执行：输入环 128 条、通知环 64 条、外层事务最多 256 步。
第 129 个输入导致整批未交付事件丢弃、封闭释放会话并报告 InputEventBufferOverflow。
监听循环、通知溢出或异常会停止当前外层事务剩余回调；下一次正常更新恢复保护状态。

## 能力与滚动

内置提供者描述 held keys、edges、已提交文本、组合与 IME 控制能力；能力位不等于严格会话支持。
Input System 额外报告 ScrollDelta 与 DeltaOnly，不保证设备分类、手势/惯性 phase、gesture ID 或 capture。
必须检查能力字段，不能按 ProviderKind 推断。

滚动仍使用同一份 uGUI PointerEventData。可选 IHybridScrollInputProvider 只能为该事件补元数据，
不是第二个 delta 来源。不匹配的 delta/无效 phase 被拒绝，不合并猜测。
DeltaOnly Elastic 用对象本地非缩放时间释放延迟；phased 提供者等待真实 gesture 和 momentum 全结束。
Unsupported 会关闭并收敛视觉 Elastic，不影响普通受限滚动。见 [限制](limitations.md#hybridinput-scroll-deltaonly)。

## 可选 Input System 程序集

核心 Milestro 不引用 Unity.InputSystem；Milestro.InputSystem 用版本 define 按范围启用。
无包时核心仍可编译并在旧输入系统可用时选择 legacy，但不能获得严格 TextInput 焦点。

Both 模式下包缺失、低于 1.16.0（含其 prerelease）、无法解析或达到 2.0.0-0 时，
精确匹配的 InputSystemUIInputModule 只得到 legacy DeltaOnly 滚动路线，Editor 给警告。
New-only 的相同情况为 Editor 错误并阻止 Player 构建。
解决方式是升级兼容包、改 Both 或改旧输入系统；后两者仍不提供严格编辑会话。
Legacy-only 不接收此警告。范围按 Unity 比较规则，超过下限的合法 1.x prerelease 可编译选择，
不代表逐个认证其运行内容；稳定 1.x 版本用于认证矩阵。

## 文本与组合顺序

commit 和 composition 是独立有序事件。commit 结束此前 preedit，同帧后来的 composition
建立剩余/下一段 preedit，支持部分提交无需多等一帧。不能假定 commit 必配空 composition 回调。

## 原生取消

失焦先使旧托管所有权失效，再请求必要的原生取消，旧回调不能进入任何新旧所有者。
严格取消失败时诊断可见，边界保持关闭直到真正空组合确认。
Windows 支持原生取消；其他平台见 [IME 限制](limitations.md#hybridinput-ime-composition-and-session-control)。

## 破坏性迁移

旧 Milestro.InputManagement.HybridInput 与 InputStringReceiver 已移除，无兼容 shim。
删除旧组件与序列化引用；需要进程级按键快照时用 HybridInputRuntime.IsKeyPressed。
私有文字帧和 key edges 没有公开消费替代，游戏输入应走应用自己的 Unity 输入层。
使用唯一 EventSystem 与匹配模块，需要编辑时安装受支持 Input System。
