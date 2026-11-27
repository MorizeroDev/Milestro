# Inline images and placeholders

[English](inline-images-design.md) | [简体中文](../../zh/development/inline-images-design.md)

`<img>` loads through providers and displays through the TextBox uGUI overlay.
`<placeholder>` only lays out geometry; the application owns its visual objects.
XML `<sprite>` borrows Unity Sprites from a provider and shares img layout and overlay rendering.
GameObjects and prefabs are not accepted. Migrate old img tags without src to placeholder.

## Resource entry point

`ImageResources.Providers` is the global `ResourceProviderRouter<IImageResourceProvider>`.
Register rejects duplicates, Replace requires an existing registration, Unregister
reports removal, and DefaultProvider is replaceable. Schemes match
`[A-Za-z][A-Za-z0-9+.-]*`, case-insensitively.

Only an initial `scheme://` selects explicit routing. Unknown schemes throw.
Other input goes unchanged to the default provider, without path classification,
trimming, or URI decoding. The default logs an error and returns null.
The image entry point additionally recognizes standard `data:` URIs and selects
the registered data provider; the general router remains unchanged.

The built-in file provider uses UnityWebRequestTexture with `file://…`.
Applications supply Addressables providers; no Addressables dependency is added.

```csharp
public interface IImageResourceProvider
{
    Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken);
}
```

Return `InlineResourceBorrow.Create(new ImageResource(sprite), returnResource)` on success, null for no resource,
or throw on failure. Providers clean up partial allocations before throwing.
The return callback captures the owner's handles; Milestro does not access them. Register, borrow, and dispose on the
Unity main thread; asynchronous I/O does not change Unity object threading rules.
Providers own cross-TextBox caching and resource destruction.
`ImageRequest.Source` preserves the original source without trimming or decoding.
`ImageResources.BorrowAsync(source)` remains a string convenience entry point and creates the request
after routing. Image and sprite providers receive ImageRequest and SpriteRequest respectively,
so one class can naturally overload both interface methods.

## Leases and lifecycle

### Sprite provider

Set `SpriteResources.Provider` to an implementation of this interface. Milestro does not prescribe
an asset format or name/index lookup protocol:

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

`SpriteRequest.Attributes` is a read-only copy of all attributes with entities decoded,
including layout attributes consumed by Milestro. Custom attributes pass through; even `src`
does not invoke image-provider routing here. `Content` is an XML fragment without the outer tag,
preserving nested elements, text, whitespace, comments, and CDATA. The XML reader may normalize
quotes, entity spelling, and line endings; this is not a byte-for-byte source representation.
Only the provider consumes the content: it does not enter text layout, acquire nested resources,
or become fallback text. Both empty-element and paired tags occupy one inline object position.

Both providers return `InlineResourceBorrow` while retaining distinct request interfaces.
`InlineResourceBorrow` is an abstract base class. Use `Create(resource, onReturn)` for the default
implementation, or derive a class, call `base(resource)`, and override `ReturnCore()` to encapsulate
state. Dispose is implemented by the base class and marks IsDisposed before returning, guaranteeing
one invocation even if return handling throws or reenters.
One application class can implement both. Its read-only `Resource` is an `ImageResource`
containing the Sprite, intrinsic dimensions, and suggested layout dimensions.
The Sprite must be usable on successful return; Sprite assets do not need Awake.
The provider remains the owner: Milestro never destroys the Sprite, Texture, or underlying handle.
`SpriteResources.BorrowAsync` returns `InlineResourceBorrow`. Create a distinct borrow object for
every successful request, even when sharing a Sprite/handle. Dispose invokes the captured return
callback once, including when it throws or reenters. Already disposed borrows and invalid Sprites are rejected.
Null means no resource; exceptions mean acquisition failure. Providers clean up partial resources
on failure. Configure, acquire, and release on Unity's main thread.

Sprites share the lifecycle below with img; `RefreshInlineImages()` refreshes both.
The TMPro parser also supports `<sprite=0>`, `<sprite name="coin">`, and `<sprite="items" index=0>`.
These are single inline objects without closing tags and have empty Content. A nonnegative integer
primary value maps to index; otherwise it maps to asset. Attribute names are lowercased, values preserved.
The same Sprite provider handles lookup. Exactly one name/index selector is required. Custom attributes,
CSS dimensions, and color/tint/anim are unsupported in TMP mode; these tags warn and remain literal.
TMP_SpriteAsset is not loaded automatically. Other parsers can use `ParagraphPayloadBuilder.AppendSprite(request, style)`.

`ImageResources.BorrowAsync` also returns `InlineResourceBorrow`.
Both entry points share cancellation, validity checks, and return handling.
Replacing or unregistering providers never changes existing return callbacks.
File and Data URI providers destroy their sprites and
textures; Addressables providers release their own handles.

Each producer binds resources by content occurrence. Repeated images acquire
separate leases; auto sizing, measurement, and relayout reuse bindings.
Shared parsers do not perform I/O or retain resource state.

- Content/parser changes, producer disable, and destruction cancel and release.
- Late successful results after cancellation are released without updating stale bindings.
- Disabling only TextBox hides views while its producer retains resources.
- Scrolling offscreen, visibility, and opacity do not unload resources.
- `TextBox.RefreshInlineImages()` and its producer equivalent release and retry.
  Registration changes do not automatically refresh existing content.
- Failures log once rather than retrying each frame.

Child MaskableGraphics follow scrolling, scaling, writing mode, and ancestor masks.
Rendering uses Sprite vertices, triangles, and UVs, including off-center pivots, trimmed meshes,
and atlas UVs. Clipping interpolates UVs instead of assuming an axis-aligned atlas rectangle.
Tests cover imported Tight meshes and clipped sub-texture UVs; rotated atlas packing still needs
verification with actual packed resources.
They clip to the content box and text viewport and do not intercept raycasts.
In flow mode the overlay is positioned after the visible range is applied for
the frame, so it stays aligned with the text slice within the same frame.
The overlay maps submitted output coordinates into the same current flow bounds
as the text mesh, including fractional viewport heights. Using the snapshot's
older flow range for only the overlay makes images drift against the text.
Image presentation uses the accepted render submission's geometry immediately:
the text texture can update before its completion callback reaches the main
thread. Waiting for the callback leaves images several frames behind the text.
Link hit testing still uses completed, published geometry. Without a pending
submission, images use published geometry; transient invalidation retains the
last presentation for the same content version.
Pixel readback tests compare rendered text and image positions on every scroll
frame, including reverse scrolling, internal scrolling, and atlas/non-atlas output.
Public view factories/binding events are not available; use placeholders for
custom interactive views.

## Tags and CSS

```xml
<placeholder key="custom-view" width="24" height="24"/>
<img src="addressable://icons/coin" style="height: 1em; object-fit: contain"/>
<img src="icons/coin" width="24" height="24"/>
```

| Property | Support |
| --- | --- |
| width, height | Positive px/em; images also support CSS auto |
| margin and physical longhands | Nonnegative px/em, 1–4-value shorthand |
| vertical-align | middle (default), top, bottom, baseline |
| object-fit | contain (default), cover, fill, none, scale-down |
| object-position | One or two directional keywords/percentages, center by default |
| opacity | Finite values clamped to 0–1 |
| visibility | visible/hidden, retaining layout space |

Fitting, positioning, and opacity affect image presentation only. Visibility
inherits from ancestor styles and supports overrides; it affects inline objects,
not ordinary text painting.

CSS dimensions override attributes. Placeholders require two explicit dimensions.
Images and sprites with one explicit dimension derive the other from intrinsic aspect ratio,
ignoring provider suggestions. With both auto, provider suggestions apply; if only one is suggested,
the other follows the intrinsic ratio. Without suggestions, height is 1em and width follows the ratio,
initially 1:1 until loaded.
Completion invalidates measurement/layout. Em follows effective font size.

`ImageResource.IntrinsicSize` comes from `Sprite.rect.size` and describes texture pixels.
It supplies the aspect ratio, not a default layout height; `pixelsPerUnit` does not convert layout units.
Both image and sprite providers can supply optional `suggestedWidth` / `suggestedHeight`:

```csharp
return InlineResourceBorrow.Create(
    new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(1.2f)),
    () => owner.Return(handle));
```

`InlineResourceLength.Pixels(24)` expresses fixed layout pixels; `Em(1.2f)` scales with effective
font size. Values must be finite and positive; a default struct value is invalid.
Two suggestions can define a box with a different aspect ratio; object-fit still controls presentation.

Margins are part of native placeholder dimensions and do not collapse.
Geometry Rect is the content box, OuterRect includes margins, Source identifies
the resource, key identifies business content, and id identifies geometry.
Visible combines style and viewport visibility.

Horizontal baseline aligns the bottom edge to the alphabetic baseline, top/bottom
align to line edges, and middle centers. Vertically, top aligns right, bottom
aligns left, middle centers, and baseline places the left edge at the column
baseline, growing thickness to the right. Objects do not rotate.
Objects inside Ruby remain ignored with an error.

Percentage dimensions, negative margins, min/max sizing, padding, backgrounds,
borders, rounded corners, floats, absolute positioning, and full display are unsupported.

## Data URIs and SVG

DataUriImageResourceProvider accepts `data:image/…`, with base64 or percent-encoded
binary bytes. Plus signs stay literal. PNG/JPEG and other raster formats use Skia
decoding and RGBA readback, preserving orientation and straight alpha.

MIME `image/svg+xml` selects SkSVGDOM instead of a bitmap codec. SVG uses root
intrinsic dimensions, then viewBox dimensions when incomplete, then 300×150.
Rasterization occurs once; enlarging does not rerender vectors. External SVG
resources have no additional loading provider. SVG raster dimensions are limited
to 16384 per axis. URI-encode SVG before XML-escaping attributes.

## Native pairing and validation

MilestroImage.ReadRgbaPixels and Svg.Rasterize use new C exports. Deploy managed and
native plugins together. Generate bindings with `./gradlew h2cs`, never by hand.

Tests cover routing, original-provider and idempotent release, late cancellation,
repeated occurrences, CSS/writing modes, Data URI bytes, raster orientation,
SVG pixels, file ownership, and overlay lifecycle.
