# 内联图片与占位符

[English](../../en/development/inline-images-design.md) | [简体中文](inline-images-design.md)

`<img>` 通过资源 Provider 加载图片，由 TextBox 的 uGUI Overlay 显示。
`<placeholder>` 只排版并提供几何，应用拥有其视觉对象。`<sprite>` 尚未实现。
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
    Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken);
    void Release(ImageResource resource);
}
```

成功时返回 `new ImageResource(sprite, releaseToken)`；null 表示没有资源，异常表示失败。
Provider 自行清理获取失败前的部分资源。ReleaseToken 可保存句柄或底层纹理。
注册、获取和释放在 Unity 主线程调用；异步 I/O 不改变 Unity 对象的线程要求。
跨 TextBox 缓存由 Provider 实现，管理器不自动缓存或销毁底层资源。

## 租约与生命周期

`ImageResources.AcquireAsync` 返回引用类型 `ImageResourceLease`。
Provider 属性固定为原实例，Resource 是只读资源描述；Dispose 幂等，只调用一次 Release。
替换或注销 Provider 不影响已有租约。文件和 Data URI Provider 销毁自己创建的
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

CSS 尺寸覆盖标签属性。placeholder 必须有两个明确尺寸。img 指定一边时用固有比例
推导另一边；两边 auto 时高度 1em、宽度按比例。加载前比例暂用 1:1。
资源就绪会刷新测量和布局。em 使用有效字号，随自动字号改变。

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
