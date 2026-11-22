# Developer documentation

[English](README.md) | [简体中文](../../zh/development/README.md)

These pages cover extending Milestro and working on its source. For installing
the plugin and using its components, see the [user manual](../README.md).

- [Build and package from source](build.md): dependencies, generated bindings, and CI.
- [Text pipeline](text-pipeline.md): preparation, layout reuse, font caches, and glyph meshes.
- [Text pipeline validation](text-pipeline-validation.md): Player benchmark and image checks.
- [Custom rich-text parsers](rich-text-parsers.md): registration and paragraph payloads.
- [Hybrid Input](hybrid-input.md): provider selection and input ownership.
- [TextInput accessibility design](accessibility-design.md): platform model and Unity integration.

Managed code and native plugins must come from the same revision. When the C API
changes, regenerate bindings with `./gradlew h2cs` rather than editing the outputs.
