using System;
using System.Collections.Generic;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Skia
{
    public sealed class UnitySkiaRenderCommandList
    {
        internal enum CommandKind
        {
            Paragraph = 1,
            Image = 2,
            InputBoxSnapshot = 3
        }

        internal struct Command
        {
            public CommandKind Kind;
            public IntPtr Resource;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public object KeepAlive;
            public bool SnapshotInputBox;
        }

        private readonly List<Command> commands = new List<Command>();

        internal IReadOnlyList<Command> Commands => commands;

        public int Count => commands.Count;

        public void DrawParagraph(Paragraph paragraph, Vector2 position)
        {
            if (paragraph == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.Paragraph,
                Resource = paragraph.Ptr,
                X = position.x,
                Y = position.y,
                KeepAlive = paragraph
            });
        }

        public void DrawImage(MilestroImage image, Rect rect)
        {
            if (image == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.Image,
                Resource = image.Ptr,
                X = rect.x,
                Y = rect.y,
                Width = rect.width,
                Height = rect.height,
                KeepAlive = image
            });
        }

        public void DrawImage(MilestroImage image, Vector2 position)
        {
            if (image == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.Image,
                Resource = image.Ptr,
                X = position.x,
                Y = position.y,
                KeepAlive = image
            });
        }

        public void DrawInputBox(TextLayout.InputBox inputBox, Rect viewport)
        {
            if (inputBox == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.InputBoxSnapshot,
                X = viewport.x,
                Y = viewport.y,
                Width = viewport.width,
                Height = viewport.height,
                KeepAlive = inputBox,
                SnapshotInputBox = true
            });
        }

        public void Clear()
        {
            commands.Clear();
        }
    }
}
