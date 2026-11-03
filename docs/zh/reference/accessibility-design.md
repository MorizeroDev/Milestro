# TextInput 无障碍设计

[English](../../en/reference/accessibility-design.md) | [简体中文](accessibility-design.md)

`Milestro.Components.TextInputAccessibility` 已实现，用于补足 TextBoxAccessibility 能发布静态文本/链接、
但 TextInput 过去没有值、选区、光标或编辑状态节点的缺口。

## 平台模型与 Unity 能力

主流 UIA、Apple Accessibility、ATK、Android 和 Web 文本框均有角色/值/状态、结构化选区、
辅助技术编辑动作和值/选区/光标/组合事件流。Unity 6000.3 的内置 Accessibility 模块提供：

| Unity API | 能力 |
| --- | --- |
| TextField / SearchField role | 身份 |
| label / hint、string value | 名称、提示、唯一结构化文字值 |
| Disabled/Selected/Expanded state | 有限状态，不含只读/密码/多行 |
| frame / frameGetter | 屏幕框 |
| isFocused、focusChanged | 屏幕阅读器焦点 |
| invoked / dismissed | 请求进入或离开编辑 |
| allowsDirectInteraction | 编辑时触摸直达光标 |
| SendAnnouncement(string) | 语音公告 |

Unity 没有结构化 caret/selection、粒度移动通知、只读/密码标志或插入删除语义，
当前只能将变化压入 SendAnnouncement；AT 主动改文字也不可用。

## 节点发布

TextInputAccessibility 与 TextInput 同对象，在 Unity 6000.3+ 且存在 Accessibility 模块时编译。
缺模块时与 TextBoxAccessibility 一样保留导航并告警。

- 每个输入框发布一个 TextField，不含额外容器；role 暂不映射 SearchField。
- label 优先序列化值、uGUI Text 占位文本、GameObject 名称；不读取 TextBox/TMP placeholder。
- value 为已提交文字；maskInput 时每个字素簇用一个遮罩符，暴露长度但隐藏内容。
- 编辑中 state 为 Selected；只读通过 hint 表达而非 Disabled；没有多行状态。
- frameGetter 返回屏幕空间框；编辑中 allowsDirectInteraction 为 true。
- invoked 走同一指针编辑路径，dismissed 结束编辑并清除 EventSystem 选择。

它不订阅屏幕阅读器 focusChanged；编辑会话快照决定公告时机。

## 快照差分与公告

每帧比较 Text、Cursor.Utf16Offset、选区 UTF-16 范围、组合状态与 dispatcher 聚焦状态：

- 开始/结束编辑：公告“editing”/“editing ended”。
- 值变化：按共同前后缀播报插入/删除文本；过长时播报数量，以字素簇计。
- 光标移动且无选区：按 UTF-16 偏移变化选单字符、行或词回声，不能区分键盘与指针。
- 选区变化：短范围播报内容，长范围播报字素簇数量；仅折叠选区而光标不动时不单独公告。
- IME preedit 静默，提交后走值差分。
- 密码值变化只公告一个本地化密码字符，不播明文；遮罩选区仍报字素数。

每帧至多生成一次公告并立即发送，无速率限制或分类合并。

## 局限

结构化选区需 Android AccessibilityNodeInfo extras、iOS UIAccessibility 文本输入协议等原生桥接；
该公告语义可由桥接替换为结构数据而无需改变组件模型。
辅助技术直接编辑文字、盲文设备光标/选区框、SearchField、多行 hint、禁用态映射均未实现。
占位标签只读取 uGUI Text。指针移动当前也会产生光标回声，移动原因过滤需设备验证。
enabled 时每帧同步 value，某些阅读器可能在聚焦时重复朗读。
