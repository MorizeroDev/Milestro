# 内联图片与占位符

[English](../../en/development/inline-images-design.md) | [简体中文](inline-images-design.md)

`<img>` 通过资源 Provider 加载图片，由 TextBox 的 uGUI Overlay 显示。
`<placeholder>` 只排版并提供几何，应用拥有其视觉对象。XML `<sprite>` 从 Provider 借用 Unity Sprite，
与 img 共享排版及 Overlay；不接收 GameObject 或 Prefab。
旧版不带 src 的 img 必须迁移为 placeholder。

## 资源入口

`ImageResources.Providers` 是全局 `ResourceProviderRouter<IImageResourceProvider>`。
Register 拒绝重复；Replace 要求已有注册；Unregister 返回是否移除；DefaultProvider 可替换。
scheme 使用 `[A-Za-z][A-Za-z0-9+.-]*`，不区分大小写。

仅字符串开头的 `scheme://` 选择显式 Provider，未知 scheme 抛出异常。
其余输入原样交给默认 Provider，不判断绝对/相对路径，不裁剪空白或解码 URI。
默认 Provider 记录错误并返回 null。图片入口额外识别标准 `data:` URI，
路由到注册的 data Provider；通用 Router 的规则保持不变。

内置 file Provider 使用 UnityWebRequestTexture，通过 `file://…` 加载。
Addressables 需要应用自行注册，不引入对应包依赖。

```csharp
public interface IImageResourceProvider
{
    Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken);
}
```

成功时返回 `InlineResourceBorrow.Create(new ImageResource(sprite), returnResource)`；null 表示没有资源，异常表示失败。
`ImageRequest.Source` 保留原始来源字符串，不裁剪或解码。`ImageResources.BorrowAsync(source)`
仍是字符串便捷入口，路由之后创建 ImageRequest 交给 Provider。
图片与精灵 Provider 分别接收 ImageRequest 和 SpriteRequest，同一个类可自然重载两个接口方法。
Provider 自行清理获取失败前的部分资源。归还回调捕获 owner 的句柄，Milestro 不接触底层句柄。
注册、获取和释放在 Unity 主线程调用；异步 I/O 不改变 Unity 对象的线程要求。
跨 TextBox 缓存由 Provider 实现，管理器不自动缓存或销毁底层资源。

## 租约与生命周期

### Sprite Provider

应用设置 `SpriteResources.Provider`，实现以下接口。Milestro 不规定资产格式、名称或索引协议：

```csharp
public interface ISpriteResourceProvider
{
    Task<InlineResourceBorrow?> BorrowAsync(SpriteRequest request, CancellationToken cancellationToken);
}
```

```xml
<sprite name="coin"/>
<sprite pack="items" style="height: 1.2em; vertical-align: middle">
  <item id="sword_001" rarity="rare"/>
</sprite>
```

`SpriteRequest.Attributes` 是全部属性的只读副本，属性实体已解码；包含 Milestro 消费的排版属性。
自定义属性原样传递，`src` 也不会在这里触发图片 Provider 路由。
`Content` 是不含外层标签的 XML 片段，保留嵌套元素、文本、空白、注释和 CDATA。
XML reader 可能规范化引号、实体表示和换行，因此不保证源码逐字节一致。
内部内容只由 Provider 消费，不参与正文排版、不产生嵌套资源请求，也不作为失败替代文字。
空元素和显式空内容均可用，整个 sprite 占一个正文对象位置。

两个 Provider 保留不同的请求接口，共同返回 `InlineResourceBorrow`；应用可用一个类实现两个接口。
`InlineResourceBorrow` 是抽象基类，常规用法通过 `Create(resource, onReturn)` 创建默认实现。
需要封装状态时可继承它，通过 `base(resource)` 提供资源并重写 `ReturnCore()`；
不需要自己实现 Dispose。基类在归还之前标记 IsDisposed，保证异常和重入时也只执行一次。
借用对象的 `Resource` 是只读 `ImageResource`，包含 Sprite、固有尺寸和建议排版尺寸。
Sprite 在成功返回时必须可用；它是资源对象，不需要 Awake。
Provider 始终是资源 owner，Milestro 不销毁 Sprite、Texture 或底层 handle。
`SpriteResources.BorrowAsync` 返回 `InlineResourceBorrow`；每次成功借用必须创建独立的借用对象，
即使共享同一个 Sprite/handle，也分别归还。Dispose 幂等，调用该次借用捕获的归还回调。
归还回调抛出异常或重入时也不会再次执行；已归还或 Sprite 无效的借用被拒绝。
null 表示没有资源，异常表示获取失败；获取失败之前的部分资源由 Provider 清理。
注册、获取和归还均在 Unity 主线程执行。

Sprite 与 img 使用下述相同生命周期，`RefreshInlineImages()` 同时刷新两者。
TMPro 解析器也支持 `<sprite=0>`、`<sprite name="coin">`、`<sprite="items" index=0>`。
它们是单个内联对象，无需结束标签，Content 为空。主值为非负整数时映射为 index，
否则映射为 asset；属性名转小写，值保留。资源查找由同一个 Sprite Provider 负责。
name/index 必须且只能提供一个。TMP 模式不接受自定义属性或 CSS 尺寸；
color/tint/anim 暂不支持，包含这些属性的标签警告并保留原文。不自动读取 TMP_SpriteAsset。
其他 parser 可通过 `ParagraphPayloadBuilder.AppendSprite(request, style)` 使用相同链路。

`ImageResources.BorrowAsync` 同样返回 `InlineResourceBorrow`。
两个入口共享取消、有效性检查及归还逻辑；替换或注销 Provider 不影响已有借用的回调。
文件和 Data URI Provider 在自己的归还回调中销毁自己创建的
Sprite/Texture；Addressables Provider 应释放自己的句柄。

每个 producer 按内容中的出现位置建立绑定。重复图片分别获取租约；
自动字号、重复测量与布局复用绑定。共享解析器不执行 I/O，也不保留资源状态。

- 内容或解析器变化、producer 禁用和销毁取消请求并释放资源。
- 取消后的迟到成功结果仍释放，不写回已失效绑定。
- 只禁用 TextBox 会隐藏 View，producer 保留资源供重新启用。
- 滚出视口、隐藏或透明不会卸载资源；回收 View 时清空引用。
- `TextBox.RefreshInlineImages()` 和 producer 同名方法释放旧绑定后重试。
  修改注册表不自动刷新已有内容。
- 失败记录一次，不每帧重试。

Overlay 绘制使用 Sprite 自身的顶点、三角形及 UV，处理非中心 pivot、裁切网格及图集 UV；
裁剪时插值 UV，不将图集区域假定为轴对齐矩形。
Overlay 使用子级 MaskableGraphic，跟随滚动、缩放、横竖排和父级遮罩，
同时裁剪到内容框及文本视口，默认不拦截射线。Flow 模式下，Overlay 在当帧可见
范围更新之后定位，因此同一帧内与文本切片保持一致。Overlay 将已提交的输出坐标映射
到文本网格使用的当前 Flow 范围，包括小数视口高度。仅让图片使用快照中的旧范围会
造成图片与文字相对偏移。图片立即使用已接受渲染提交的几何：文字纹理可能先于完成
回调抵达主线程而更新，等待回调会让图片落后文字数帧。链接命中仍使用已完成渲染的
已发布几何。没有待完成提交时，图片使用已发布几何；短暂失效时保留相同内容版本的
上次显示。像素回读测试逐帧比较文字和图片位置，覆盖反向滚动、内部滚动及图集开关。
尚无公开 View 工厂和绑定事件；自定义交互 View 使用 placeholder。

## 标签与 CSS

```xml
<placeholder key="custom-view" width="24" height="24"/>
<img src="addressable://icons/coin" style="height: 1em; object-fit: contain"/>
<img src="icons/coin" width="24" height="24"/>
```

| 属性 | 支持 |
| --- | --- |
| width、height | 正数 px/em；img 还支持 CSS auto |
| margin 及四侧属性 | 非负 px/em，简写 1–4 个值 |
| vertical-align | middle（默认）、top、bottom、baseline |
| object-fit | contain（默认）、cover、fill、none、scale-down |
| object-position | 一或两个方向关键字/百分比，默认 center |
| opacity | 有限数值限制到 0–1 |
| visibility | visible、hidden，保留占位 |

object-fit、object-position、opacity 仅作用于图片显示。visibility 从祖先样式继承，
允许元素覆盖；此能力针对内联对象，不改变普通文字绘制。

CSS 尺寸覆盖标签属性。placeholder 必须有两个明确尺寸。img 和 sprite 指定一边时用固有比例
推导另一边，忽略 Provider 建议尺寸。两边 auto 时先使用 Provider 建议尺寸；建议仅一边时
另一边按固有比例推导。没有建议时高度 1em、宽度按比例。加载前比例暂用 1:1。
资源就绪会刷新测量和布局。em 使用有效字号，随自动字号改变。

`ImageResource.IntrinsicSize` 来自 `Sprite.rect.size`，描述纹理像素尺寸，只提供固有比例，
不把像素高度自动当作排版高度，也不使用 `pixelsPerUnit` 换算排版单位。
img 和 sprite 的 Provider 都可返回可选 `suggestedWidth` / `suggestedHeight`：

```csharp
return InlineResourceBorrow.Create(
    new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(1.2f)),
    () => owner.Return(handle));
```

`InlineResourceLength.Pixels(24)` 表示固定排版 px，`Em(1.2f)` 表示随有效字号缩放。
数值必须有限且大于零；默认结构体值无效。两个建议尺寸都给出时允许不同于固有比例，
实际图像如何适配排版框仍由 object-fit 决定。

margin 包含在原生占位尺寸中，不合并。几何快照 Rect 是内容框，OuterRect 包含 margin；
Source 标识资源，key 用于业务，id 用于几何查询。Visible 同时反映样式和视口可见性。

横排 baseline 将框底边对齐字母基线；top/bottom 对齐行框边缘，middle 居中。
竖排 top 对齐列右边、bottom 对齐左边、middle 居中；baseline 将框左边放在列基线，
向右增长列厚。对象不旋转。Ruby 内对象仍忽略并报错。

不支持百分比尺寸、负 margin、min/max 尺寸、padding、背景、边框、圆角、float、
绝对定位及完整 display 模型。

## Data URI 与 SVG

DataUriImageResourceProvider 接受 `data:image/…`，支持 base64 和百分号编码二进制。
加号保持字面含义。PNG/JPEG 等由 Skia 解码，再读取 RGBA 生成 Unity 纹理，
保留上下方向及非预乘 Alpha。

`image/svg+xml` 按 MIME 单独走 SkSVGDOM，不能交给普通位图解码器。
SVG 优先使用根节点固有尺寸，缺少完整尺寸时使用 viewBox，否则使用 300×150。
首版只栅格化一次，放大不自动重新渲染；外部 SVG 资源没有额外加载 Provider。
SVG 光栅每边最多 16384。XML 属性中的 SVG 先 URI 编码，再进行 XML 转义。

## 原生与验证

MilestroImage.ReadRgbaPixels 和 Svg.Rasterize 使用新增 C API，原生库和托管插件必须配套。
绑定通过 `./gradlew h2cs` 生成，不得手工编辑。

测试覆盖路由、替换后释放、幂等释放、迟到取消、重复出现、CSS 与横竖排、
Data URI 字节、位图方向、SVG 像素、文件释放和 Overlay 生命周期。
Sprite 测试另外覆盖内部 XML 结构、Provider 建议尺寸、真实导入的 Tight 网格及子纹理 UV 裁剪。
旋转打包图集仍需实际打包资源验证。
