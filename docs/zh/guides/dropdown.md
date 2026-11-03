# 单选下拉框

[English](../../en/guides/dropdown.md) | [简体中文](dropdown.md)

`MilestroDropdown` 是基于索引的 uGUI 单选控件，标题、占位文字、箭头和选项行均使用 CanvasSlimText。
它不依赖 TMP、不创建隐藏 Unity Text、不启动文本编辑、IME 或软键盘。

## 创建与初始化

选择 **GameObject → UI → Milestro Dropdown**，生成背景、标题、占位、箭头、可滚动模板、
行文字、选中标记和滚动条。保持模板 inactive，展开时才实例化。场景需 EventSystem，推荐 InputSystemUIInputModule。

```csharp
dropdown.SetOptions(new[] { "低", "中", "高" }, initialIndex: 1);
dropdown.onValueChanged.AddListener(SaveQuality);
```

上例的 `SaveQuality(int index)` 由应用实现。初始化和整表替换静默；控件复制输入字符串，
`options` 为只读视图，`optionCount`、`GetOptionText(index)` 用于查询。重复文本仍是不同索引，
null 列表转为空表，null 项转为空串。

## 值与文字契约

| 成员 | 行为 |
| --- | --- |
| `value` | 已提交索引，规范化后实际变化才发一次事件 |
| `SetValueWithoutNotify(index)` | 修改值与标题但不通知 |
| `SetOptions(strings, initialIndex = 0)` | 原子替换列表，关闭弹出，丢弃候选，不发事件 |
| `ClearOptions()` | 静默清空，value 为 -1 |
| `SetOptionText(index, text)` | 刷新标题和已展开行，不改值、不发事件 |
| `Show()`、`Hide()`、`isExpanded` | 打开、取消、查询，重复调用幂等 |
| `placeholder`、`placeholderText` | 无选择文本及其 CanvasSlimText |
| `captionText`、`template`、`maxVisibleItems` | 呈现引用和最大可见行数 |
| `interactable`、`navigation`、`Select()` | 继承 Selectable，选中不自动展开 |

非空列表中负索引规范为 -1，越界正索引规范到最后一行。空表固定 -1；有选项时也可明确无选择。
展开从已提交值开始高亮，无选择时从第零行开始。悬停和 Up/Down 只改候选；确认先关闭再提交，
确认原值不发事件。Escape、外部点击和 Hide 丢弃候选。展开时程序改 value 同时更新候选，避免恢复旧值。

翻译系统可保存控件与行索引，语言改变时调用 SetOptionText；控件不保存语言键，也不实现翻译服务。

## 输入与焦点

只处理活动模块的 uGUI 事件，不额外轮询键盘。左键、触摸点击或 Submit 展开，展开后 Submit 确认。
Up/Down 不循环并自动滚入视图，其余导航在弹出期间被消费。Cancel 取消，默认 Input System 动作通常为
Enter/Escape；Space 是否提交由项目 Submit action 配置决定。

ScrollRect 处理滚轮和拖动，拖动阈值抑制滚动后的误点击。全 Canvas blocker 消费用于关闭的外部点击，
下一次点击才作用于下面控件。确认/取消后，仅在无其他对象取得选择时恢复下拉框选择。

每个 root Canvas 同时只开一个弹出。禁用交互、屏蔽射线、停用、销毁、换表或打开另一个下拉框均清理而不发值事件。
移动、缩放或改变显示几何会安全关闭，避免点击区域脱离控件。

## 弹出布局与样式

列表挂到 root screen-space Canvas，越过设置行遮罩，使用独立排序 Canvas；内部 viewport 裁剪行。
布局按 root Canvas 和 safe area 计算，可向上打开、限制位置并缩小视口，保持所有行可滚达。
支持 Overlay、Camera、嵌套与缩放 Canvas，不支持 World Space Canvas。

默认最多显示八行，标题和行均固定单行，长文本裁剪，不解析富文本、不换行、不扩大设置行。
通过各自 SlimTextRenderTextureProducer 独立配置样式。做 prefab 时复制完整生成层级，保留内部原型与 ScrollRect 引用。
行在展开时创建，只在显式变化时更新；非虚拟化列表，支持 100 行，大列表需实测性能。

物理移动设备的 safe area、触摸与性能尚未验证。手柄专用适配也未完成，
普通 uGUI 动作能操作不代表完整手柄支持；仍需验证焦点恢复、长按重复、每次按下单次激活和输入方式切换。
