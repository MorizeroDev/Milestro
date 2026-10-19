using System;
using System.Collections.Generic;
using Milestro.Util;
using UnityEngine;
using Paragraph = Milestro.Skia.TextLayout.Paragraph;
using SkFont = Milestro.Skia.Font;

namespace Milestro.Skia
{
    public sealed class UnitySkiaRenderCommandList
    {
        internal enum CommandKind
        {
            Paragraph = 1,
            Image = 2,
            InputBoxSnapshot = 3,
            SlimText = 4,
            BeginRegion = 5,
            EndRegion = 6
        }

        internal enum ResourceOwnership
        {
            None = 0,
            Paragraph = 1,
            InputBoxSnapshot = 2
        }

        internal struct Command
        {
            public CommandKind Kind;
            public IntPtr Resource;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float ClipX;
            public float ClipY;
            public float ClipWidth;
            public float ClipHeight;
            public float VisualOffsetX;
            public float VisualOffsetY;
            public object KeepAlive;
            public bool SnapshotInputBox;
            public Func<Paragraph> ParagraphSnapshotFactory;
            public bool SnapshotParagraph;
            public ResourceOwnership Ownership;
            public bool SnapshotSlimText;
            public string Text;
            public Color32 Color;
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
                Resource = paragraph.NativePtr,
                X = position.x,
                Y = position.y,
                KeepAlive = paragraph
            });
        }

        public void DrawParagraph(Paragraph paragraph, Vector2 position, Rect clipRect)
        {
            if (paragraph == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.Paragraph,
                Resource = paragraph.NativePtr,
                X = position.x,
                Y = position.y,
                ClipX = clipRect.x,
                ClipY = clipRect.y,
                ClipWidth = clipRect.width,
                ClipHeight = clipRect.height,
                KeepAlive = paragraph
            });
        }

        internal void DrawParagraphSnapshot(Func<Paragraph> paragraphFactory, Vector2 position, Rect clipRect)
        {
            if (paragraphFactory == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.Paragraph,
                X = position.x,
                Y = position.y,
                ClipX = clipRect.x,
                ClipY = clipRect.y,
                ClipWidth = clipRect.width,
                ClipHeight = clipRect.height,
                ParagraphSnapshotFactory = paragraphFactory,
                SnapshotParagraph = true
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
                Resource = image.NativePtr,
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
                Resource = image.NativePtr,
                X = position.x,
                Y = position.y,
                KeepAlive = image
            });
        }

        public void DrawInputBox(TextLayout.InputBox inputBox, Rect viewport, Vector2 visualOffset)
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
                VisualOffsetX = visualOffset.x,
                VisualOffsetY = visualOffset.y,
                KeepAlive = inputBox,
                SnapshotInputBox = true
            });
        }

        public void DrawInputBox(TextLayout.InputBox inputBox, Rect viewport)
        {
            DrawInputBox(inputBox, viewport, Vector2.zero);
        }

        public void DrawString(string text, SkFont font, Vector2 baselinePosition, Color32 color)
        {
            if (font == null)
            {
                return;
            }

            commands.Add(new Command
            {
                Kind = CommandKind.SlimText,
                Resource = font.NativePtr,
                X = baselinePosition.x,
                Y = baselinePosition.y,
                KeepAlive = font,
                SnapshotSlimText = true,
                Text = text ?? "",
                Color = color
            });
        }

        public void Clear()
        {
            commands.Clear();
        }

        internal CapturedCommands Capture()
        {
            var captured = new CapturedCommands();
            try
            {
                CaptureInto(captured);
                return captured;
            }
            catch
            {
                captured.Dispose();
                throw;
            }
        }

        internal void CaptureInto(CapturedCommands captured)
        {
            foreach (var source in commands)
            {
                    var command = source;
                    if (command.SnapshotParagraph)
                    {
                        var snapshot = command.ParagraphSnapshotFactory();
                        captured.OwnedResources.Add(snapshot);
                        command.Resource = snapshot.NativePtr;
                        command.KeepAlive = snapshot;
                    }
                    else if (command.SnapshotInputBox)
                    {
                        var snapshot = ((TextLayout.InputBox)command.KeepAlive).CreateDrawSnapshot();
                        captured.OwnedResources.Add(snapshot);
                        command.Resource = snapshot.NativePtr;
                        command.KeepAlive = snapshot;
                    }
                    else if (command.SnapshotSlimText)
                    {
                        var snapshot = new TextDrawSnapshot((SkFont)command.KeepAlive, command.Text, command.Color);
                        captured.OwnedResources.Add(snapshot);
                        command.Resource = snapshot.NativePtr;
                        command.KeepAlive = snapshot;
                    }
                    command.SnapshotParagraph = false;
                    command.SnapshotInputBox = false;
                    command.SnapshotSlimText = false;
                    command.ParagraphSnapshotFactory = null!;
                    captured.Commands.Add(command);
            }
        }

        internal void AppendRegion(CapturedCommands captured, RectInt allocation, RectInt content, float scale)
        {
            commands.Add(new Command
            {
                Kind = CommandKind.BeginRegion,
                X = allocation.x,
                Y = allocation.y,
                Width = allocation.width,
                Height = allocation.height,
                ClipX = content.x,
                ClipY = content.y,
                ClipWidth = content.width,
                ClipHeight = content.height,
                VisualOffsetX = scale
            });
            commands.AddRange(captured.Commands);
            commands.Add(new Command { Kind = CommandKind.EndRegion });
        }

        internal sealed class CapturedCommands : IDisposable
        {
            internal readonly List<Command> Commands = new List<Command>();
            internal readonly List<IDisposable> OwnedResources = new List<IDisposable>();

            public void Dispose()
            {
                foreach (var resource in OwnedResources)
                {
                    resource.Dispose();
                }
                OwnedResources.Clear();
                Commands.Clear();
            }
        }
    }
}
