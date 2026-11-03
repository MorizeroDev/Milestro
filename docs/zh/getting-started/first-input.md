# 创建第一个输入框

[English](../../en/getting-started/first-input.md) | [简体中文](first-input.md)

`Milestro.Components.TextInput` 是可编辑 uGUI 组件，自行处理光标、选区、组合文本、剪贴板快捷键、
滚动和移动软键盘。键盘与 IME 事件经由进程级 HybridInput 分发器传递。
应添加 `TextInput`；`InputBox` 是它拥有的底层编辑模型，不是场景组件。

## 1. 配置 EventSystem

场景中只保留一个活动 `EventSystem`，输入模块使用 `InputSystemUIInputModule`。
安装 `[1.16.0,2.0.0-0)` 范围内的 `com.unity.inputsystem`，将 Active Input Handling 设为
**Input System Package (New)** 或 **Both**。

`StandaloneInputModule` 无法提供可编辑 `TextInput` 所需的严格焦点会话。
提供者选择和迁移见 [HybridInput](../reference/hybrid-input.md)。

## 2. 创建组件

1. 在 Canvas 下创建 UI 对象，添加 **Milestro → Text Input**。
2. 设置非零 `RectTransform`，例如 `600 × 80`。
3. 在 Inspector 中设置 `Size`、`Text Color`、`Caret Color`。
4. 单行输入保持 `Line Mode = Single Line`。

无需另建子 `Text`、`TMP_InputField` 或 `InputBox`。组件自行绘制值、光标和选区。
可选 `Placeholder` 接受独立的 uGUI `Graphic`，如 `TextBox` 或 Unity `Text`；
将它放进输入框范围并关闭其 Raycast Target，组件会控制显隐。

## 3. 监听事件

创建 `FirstInput.cs`，挂到输入对象并赋值：

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class FirstInput : MonoBehaviour
{
    [SerializeField] private TextInput input;

    private void OnEnable()
    {
        input.onValueChanged.AddListener(HandleValueChanged);
        input.onSubmit.AddListener(HandleSubmitted);
        input.onFocusGained.AddListener(HandleFocusGained);
        input.onFocusLost.AddListener(HandleFocusLost);
    }

    private void OnDisable()
    {
        input.onValueChanged.RemoveListener(HandleValueChanged);
        input.onSubmit.RemoveListener(HandleSubmitted);
        input.onFocusGained.RemoveListener(HandleFocusGained);
        input.onFocusLost.RemoveListener(HandleFocusLost);
    }

    private void HandleValueChanged(string value) => Debug.Log($"Value changed: {value}");
    private void HandleSubmitted(string value) => Debug.Log($"Submitted: {value}");
    private void HandleFocusGained() => Debug.Log("Input focused");
    private void HandleFocusLost() => Debug.Log("Input lost focus");
}
```

进入 Play Mode，点击输入框、输入文字、按 Enter。单行 Enter 在本帧值变更之后触发提交。

## 4. 读写文本

```csharp
input.Text = "初始值";
var value = input.Text;
input.SetTextWithoutNotify("静默更新");
```

`Text` 是规范化后的已提交值。赋不同值触发一次 `onValueChanged`，相同值不触发；
`SetTextWithoutNotify` 不发事件。程序赋值同样经过换行、无效 UTF-16、长度和验证规则处理。

## 5. 常见设置

| 属性 | 含义 |
| --- | --- |
| `characterLimit` | Unicode 标量数上限，`0` 不限 |
| `contentType` | 数字、邮箱、姓名、密码、PIN 等预设 |
| `keyboardType` | 移动软键盘类型 |
| `readOnly` | 禁止编辑，仍可选择文本 |
| `maskInput`、`maskChar` | 密码式遮罩 |
| `submitOnEnter` | 多行模式也可配置 Enter 提交 |
| `selectionEnabled` | 指针和键盘范围选择 |
| `lineMode`、`wrapMode`、`textOverflow` | 单/多行、换行和溢出策略 |

逐码点过滤用 `onValidateInput`；整段替换、粘贴或字素簇需要原子接受/拒绝时用 `onValidateEdit`，
详见 [输入与 IME](../guides/input.md)。

## 6. 移动软键盘

当 `TouchScreenKeyboard.isSupported` 为真时，聚焦打开软键盘。
`shouldHideMobileInput` 默认 `true`，隐藏额外的原生输入框，直接编辑 Unity 内的文本；
设为 `false` 使用平台原生输入框。用 `onTouchScreenKeyboardStatusChanged` 观察会话。

普通平台文字输入不等于严格控制原生组合取消；后者仍受平台限制，见
[已知限制](../reference/limitations.md#hybridinput-ime-composition-and-session-control)。
