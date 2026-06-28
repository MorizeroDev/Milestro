# Milestro

Milestro is a native text rendering plugin for Unity.

The goal of this project is to bypass Unity's built-in text rendering pipeline and render text directly with Skia. Milestro gives Unity projects a lower-level path for text layout, glyph processing, rasterization, and render target output while keeping a C# API available inside Unity.

## What This Project Does

Milestro provides:

- A native C++ library built around Skia text layout and rendering.
- Unity C# bindings for the native Milestro API.
- Unity components for rendering Skia paragraphs through bitmap textures, render textures, mesh geometry, and SDF data.
- Rich text parsing helpers for converting styled markup into paragraph payloads.
- Font registration and font family access through the native layer.
- Glyph extraction utilities for paths, vertices, bounds, and custom rendering workflows.
- Unity native rendering integration for platform graphics backends.

In short: Milestro lets Unity ask Skia to shape and render text, instead of relying on Unity's normal text rendering stack.

## Why

Unity's built-in text path is convenient, but it can be limiting when a project needs direct control over:

- Text shaping and paragraph layout.
- Font fallback and font registration.
- Rendering text into custom textures or render targets.
- Converting text to paths, meshes, or SDF data.

Milestro exists to make those workflows available from Unity.

## Architecture

Milestro is split into a native layer and a Unity layer.

### Native Layer

The native layer is written in C++ and exposes a C-compatible interface for Unity and generated bindings.

Main areas:

- `src/skia/` and `include/Milestro/skia/`: Skia wrappers for canvas, images, fonts, typefaces, paths, SVG, vertex data, and text layout.
- `src/icu/` and `include/Milestro/icu/`: ICU-related helpers.
- `src/game/` and `include/Milestro/game/`: exported plugin API used by Unity/C#.
- `src/unity_render/`: Unity native rendering integration.

### Unity Layer

The Unity layer lives under `apps/unity-plugins/Milestro/`.

Main areas:

- `Binding/`: C# bindings to the exported native API.
- `Skia/`: managed wrappers for native Skia objects.
- `Skia/TextLayout/`: paragraph, paragraph builder, paragraph style, text style, and font collection APIs.
- `Components/`: Unity components for rendering paragraphs.
- `RichTextParser/`: markup parsing into styled text payloads.
- `Model/`: shared text and rendering data models.
- `ColorUniverse/`: color conversion and parsing helpers.

## Repository Layout

```text
apps/
  cmd/                  Native CLI entry point
  unity-plugins/        Unity C# plugin code
cmake/                  CMake helper modules
docs/                   Project documentation
ext/                    Vendored third-party dependencies
include/Milestro/       Public native headers
scripts/                Release and maintenance scripts
src/                    Native implementation
tests/                  Native tests and test data
```

## Supported Platforms

Milestro is currently in the proof-of-concept stage.

The target Unity graphics backends are:

- Metal
- Direct3D 12
- Vulkan
- OpenGL
