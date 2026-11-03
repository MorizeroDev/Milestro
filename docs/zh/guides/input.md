# 输入与 IME

[English](../../en/guides/input.md) | [简体中文](input.md)

`TextInput` 拥有原生 `InputBox`，将值、光标和选区绘制到纹理，并通过 HybridInput 管理键盘、
组合文本和 IME 所有权。最小场景见 [第一个输入框](../getting-started/first-input.md)。
需要唯一活动 EventSystem、`InputSystemUIInputModule`、`[1.16.0,2.0.0-0)` Input System，
以及 New/Both 的 Active Input Handling。旧轮询提供者无法证明事件采集时的所有者，不能取得严格焦点。

## 外观与表单参数

| 属性 | 用途 |
| --- | --- |
| `size`、`fontFamilies`、`textColor`、`locale` | 字号、字体列表（默认 system-ui）、颜色与语言 |
| `caretColor`、`caretWidth`、`blinkInterval` | 光标外观 |
| `selectionColor`、`selectionEnabled` | 选区外观及范围选择 |
| `textAlignment` | Left、Center、Right |
| `lineMode`、`wrapMode` | SingleLine / MultiLine；单行强制 NoWrap |
| `textOverflow`、`ellipsisString`、`srgb` | 溢出、末尾字符与表面色彩空间 |
| `Text`、`SetTextWithoutNotify(string)` | 已提交规范值及静默设置 |
| `placeholder` | 值为空时显示的独立 Graphic |
| `characterLimit` | Unicode 标量数限制，0 不限，不按字素簇计数 |
| `contentType`、`characterValidation` | 预设及内置过滤 |
| `keyboardType` | 软键盘类型，不负责验证 |
| `readOnly`、`maskInput`、`maskChar`、`isPassword`、`allowCopy` | 编辑、遮罩、复制 |
| `submitOnEnter` | 多行 Enter 提交，Shift+Enter 换行 |
| `smoothScroll`、`scrollTweenDurationSeconds` | 光标跟随及滚轮动画 |
| `shouldHideMobileInput` | 隐藏平台额外输入框 |

`Submit()` 发出提交并保留焦点；监听器可转移焦点或关闭表单，随后发生正常结束编辑事件。

## 焦点、选区与只读

`readOnly` 仍允许聚焦、移动光标、选择和按规则复制，但拒绝编辑与提交，不打开软键盘。
`selectionEnabled = false` 只禁用范围选择，仍可聚焦、编辑和打开键盘。
禁用组件或对象会拒绝新焦点并结束已有会话一次，重新启用后需按正常 EventSystem 路径再次选择。

普通指针与程序选择会直接进入编辑，没有中间的“选中但未编辑”状态，也没有 TMP 式独立 `interactable` 状态。

## 事件与顺序

| 事件 | 参数 | 时机 |
| --- | --- | --- |
| `onValueChanged` | string | 已提交规范值实际变化之后 |
| `onEndEdit` | string | 焦点会话结束时的最终值 |
| `onSubmit` | string | 单行 Enter、多行配置提交、移动 Done、Submit() |
| `onFocusGained` / `onFocusLost` | 无 | 获得/失去分发器焦点 |
| `onTouchScreenKeyboardStatusChanged` | TouchScreenKeyboard.Status | 软键盘状态变更 |

事件为只读 UnityEvent，通过 `AddListener` / `RemoveListener` 注册；运行时监听器在 domain reload 后重建。
回调读取到的 `Text` 已完成提交。同值赋值不发事件，不同值赋值即使禁用时也发一次。
`SetTextWithoutNotify`、反序列化、加载场景和 Inspector 验证静默。

IME preedit 不进入 `Text`，不发值变更。真实会话结束先发一次 `onEndEdit`，再发 `onFocusLost`。
提交回调可关闭、禁用、销毁、切换或重开输入框，旧会话清理不会误关新会话。

## 验证

处理顺序：行模式规范化 → 内置 characterValidation → characterLimit → onValidateInput → onValidateEdit。

```csharp
input.onValidateInput = (proposedText, charIndex, codePoint) =>
    codePoint == ' ' ? codePoint : 0;
```

`onValidateInput` 返回接受/替换的 Unicode 码点，或 0 拒绝。ZWJ emoji 会产生多个标量回调，不是原子的。
整段粘贴或字素序列需用：

```csharp
using Milestro.Model;

input.onValidateEdit = edit =>
    edit.InsertedText == "👨‍👩‍👧‍👦"
        ? TextInputEditResult.Reject()
        : TextInputEditResult.Accept();
```

`TextInputEditContext` 包含 `CurrentText`、`ProposedText`、`InsertedText`、
`SelectionStart`、`SelectionEnd`、`HasSelection`、`Source`；来源为 Typing、Paste、ImeCommit、Programmatic。
`Accept()` 接受；`Reject()` 保持原值与选区；`Replace(text)` 替换完整候选，再经过规范化、长度与内置验证，
不递归调用 onValidateEdit。改变长度、内容规则或 onValidateInput 会重新验证现值并清空撤销历史。
preedit 在提交前不受限制。

## 内容预设

预设包含 Standard、Autocorrected、Custom、IntegerNumber、DecimalNumber、Alphanumeric、Name、
EmailAddress、Password、Pin。数字使用 ASCII 数字、可选前导负号和小数点 `.`，PIN 仅数字。
姓名支持 Unicode 字母与组合标记，并将词首大写。邮箱仅过滤字符、重复 @ 和相邻点，提交时仍需完整验证。

过滤不保证最终值可解析；空串、`-`、`.`、`-.` 需要调用方处理。小数规则固定使用 ASCII 点，
通常配合 invariant culture 格式化和解析。密码/PIN 默认遮罩并禁止复制。
除 Standard、Autocorrected、Custom 外的预设使用单行；修改 characterValidation 或 keyboardType 会切到 Custom。

## 移动软键盘

支持 `TouchScreenKeyboard` 时，聚焦使用当前类型、自动纠错、多行与遮罩等设置打开键盘。
长度限制只在托管层严格执行，不直接传给平台键盘，因为平台不保证按 Unicode 标量计数。
补充平面 emoji 是一个标量，ZWJ emoji 可包含多个标量。

`shouldHideMobileInput` 默认 true，隐藏平台原生输入框并同步 Unity 值与选区作为上下文；
false 显示平台输入框，其快照仍经过同一验证管线。Android 硬件键盘按 TMP 类似方式抑制软键盘。
失焦关闭；Done 提交并关闭；Cancel/LostFocus 关闭但不提交。
`touchScreenKeyboard` 返回当前键盘或 null，状态事件对 Visible、Done、Canceled、LostFocus 各次变更只发一次。

## 多行行为

MultiLine + NoWrap 可水平滚动，Wrap 按视口宽度软换行。默认 Enter 换行；submitOnEnter 开启后
Enter 提交、Shift+Enter 换行。用于确认 IME 组合的 Enter 不提交。

## 手柄与两阶段编辑：待实现

专用手柄适配尚未实现，也未在设备验证。其他控件的 uGUI 导航不代表 TextInput 已支持手柄编辑。
未来模型是：导航选中但不编辑 → Confirm 进入编辑且不重复提交 → 编辑时文本移动不转移表单焦点 →
Confirm 提交退出、Cancel 不提交退出 → 返回仍选中该框的 UI 导航。
需验证重复按键、输入方式切换、输入框切换、失焦与重入。

潜在键盘映射是未编辑时方向键导航、Enter 进入，编辑时方向键移动光标、Enter 确认、Escape 退出。
`TextInputAccessibility` 继承 Selectable，需额外移动事件仲裁；当前未实现或验证。
没有手柄桥接、两阶段激活属性、释放屏障或表单导航路由。

## 底层 InputBox

`Milestro.Skia.TextLayout.InputBox` 用于自定义渲染器，拥有值、光标、选区、命中测试、撤销/重做、
组合、换行、溢出、遮罩、视口和度量。组件为你管理其生命周期与 UI 事件。
底层接口见 [组件参考](../reference/components.md)，自建封装对象需 Dispose。
