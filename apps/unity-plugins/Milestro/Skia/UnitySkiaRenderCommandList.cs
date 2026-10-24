using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Milestro.Binding;
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
            public object? KeepAlive;
            public bool SnapshotInputBox;
            public ResourceOwnership Ownership;
            public bool SnapshotSlimText;
            public string Text;
            public Color32 Color;
            public float ContentScale;
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
            DrawParagraph(paragraph, position, clipRect, 1f);
        }

        internal void DrawParagraph(Paragraph paragraph, Vector2 position, Rect clipRect, float contentScale)
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
                ContentScale = FloatUtil.IsFinite(contentScale) && contentScale > 0f ? contentScale : 1f,
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
                if (command.Kind == CommandKind.Paragraph && command.Resource != IntPtr.Zero)
                {
                    var retained = new RetainedParagraph(command.Resource);
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphRetain(command.Resource));
                    AddOwnedResource(captured, retained);
                    command.KeepAlive = null;
                }
                else if (command.SnapshotInputBox)
                {
                    if (command.KeepAlive is not TextLayout.InputBox inputBox)
                    {
                        throw new InvalidOperationException("Milestro InputBox draw command is missing its editor model.");
                    }
                    var snapshot = inputBox.CreateDrawSnapshot();
                    AddOwnedResource(captured, snapshot);
                    command.Resource = snapshot.NativePtr;
                    command.KeepAlive = snapshot;
                }
                else if (command.SnapshotSlimText)
                {
                    if (command.KeepAlive is not SkFont font)
                    {
                        throw new InvalidOperationException("Milestro slim text draw command is missing its font.");
                    }
                    var snapshot = new TextDrawSnapshot(font, command.Text, command.Color);
                    AddOwnedResource(captured, snapshot);
                    command.Resource = snapshot.NativePtr;
                    command.KeepAlive = snapshot;
                }
                command.SnapshotInputBox = false;
                command.SnapshotSlimText = false;
                captured.Commands.Add(command);
            }
        }

        private sealed class RetainedParagraph : IDisposable
        {
            private IntPtr ptr;

            internal RetainedParagraph(IntPtr ptr)
            {
                this.ptr = ptr;
            }

            public void Dispose()
            {
                var current = ptr;
                if (current == IntPtr.Zero)
                {
                    return;
                }

                ptr = IntPtr.Zero;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphDestroy(out current));
            }
        }

        private static void AddOwnedResource(CapturedCommands captured, IDisposable resource)
        {
            try
            {
                captured.OwnedResources.Add(resource);
            }
            catch
            {
                resource.Dispose();
                throw;
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
                Exception? firstException = null;
                foreach (var resource in OwnedResources)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        firstException ??= exception;
                    }
                }

                OwnedResources.Clear();
                Commands.Clear();
                if (firstException != null)
                {
                    ExceptionDispatchInfo.Capture(firstException).Throw();
                }
            }
        }
    }
}
