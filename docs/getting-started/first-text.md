# Render your first text

This page creates a `TextBox`, assigns content from a C# script, and changes its
font size and color. It is the smallest useful Milestro scene.

## 1. Create a Canvas

In the Hierarchy window, choose **GameObject** → **UI** → **Canvas**. If the
scene already has a Canvas, use it.

Milestro's UI text is a uGUI `Graphic`. It needs a Canvas and a non-zero
`RectTransform` size to produce a render texture.

## 2. Create a TextBox

1. Select the Canvas.
2. Choose **GameObject** → **Create Empty** and rename it `Hello Text`.
3. Keep it as a child of the Canvas.
4. Add the **Milestro** → **Text Box** component.
5. Set its `RectTransform` size to something visible, for example `800 × 200`.

`TextBox` automatically adds a `TextBoxRenderTextureProducer` to the same
GameObject. The producer owns the text content and the paragraph/text style
settings; `TextBox` presents the producer's render texture in the UI.

Do not look for a `text` field on `TextBox` itself. Set it on the producer:

```csharp
textBox.GetComponent<TextBoxRenderTextureProducer>().content = "Hello";
```

## 3. Add a script

Create a C# script named `FirstText.cs` and attach it to the same GameObject:

```csharp
using Milestro.Components;
using Milestro.Components.Internal;
using UnityEngine;

public sealed class FirstText : MonoBehaviour
{
    [SerializeField] private TextBox textBox = null;

    private void Start()
    {
        if (textBox == null)
        {
            Debug.LogError("Assign a TextBox to FirstText.", this);
            return;
        }

        var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
        if (producer == null)
        {
            Debug.LogError("TextBoxRenderTextureProducer was not created.", this);
            return;
        }

        producer.content = "Hello, Milestro!";
        producer.textStyle.size = 48f;
        producer.textStyle.textColor = Color.white;
    }
}
```

Drag the `Hello Text` GameObject into the script's `Text Box` field, then press
Play. The example sets white text so it is visible against the default dark
Game view background.

## 4. Change the result

The properties used most often are:

| What you want | Property |
| --- | --- |
| Change the string | `producer.content` |
| Change size | `producer.textStyle.size` |
| Change color | `producer.textStyle.textColor` |
| Change font family | `producer.textStyle.fontFamilies` |
| Change weight | `producer.textStyle.weight` |
| Change paragraph alignment | `producer.paragraphStyle.textAlign` |
| Enable or disable wrapping | `producer.wrapMode` |
| Clip, ellipsize, or overflow | `producer.textOverflow` |
| Inset the text | `producer.margin` |

For example:

```csharp
producer.content = "A longer line that should wrap at the box width.";
producer.wrapMode = Milestro.Model.TextBoxWrapMode.Wrap;
producer.textOverflow = Milestro.Model.TextOverflow.Clip;
producer.paragraphStyle.textAlign = Milestro.Model.TextAlign.Left;
producer.textStyle.fontFamilies.Clear();
producer.textStyle.fontFamilies.Add("sans-serif");
producer.textStyle.letterSpacing = 0.5f;
producer.textStyle.lineHeight = 1.25f;
```

`textStyle.fontFamilies` is a family list, not a reference to a Unity
`Font`/`TMP_FontAsset`. Milestro resolves the list through its font registry and
system fallback. See [Text, fonts, and rich text](../guides/text.md).

## 5. Where to go next

- Use a lightweight, always-single-line label with
  [SlimText](../guides/slim-text.md).
- Add an editable field with [TextInput](first-input.md).
- Learn the sizing and wrapping rules in [Layout and sizing](../guides/layout.md).
- Compare the model with TextMesh Pro in
  [Coming from TextMesh Pro](../guides/coming-from-tmpro.md).
