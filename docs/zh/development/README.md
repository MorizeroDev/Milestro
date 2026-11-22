# 开发文档

[English](../../en/development/README.md) | [简体中文](README.md)

这里介绍如何扩展 Milestro 和修改源码。安装插件、使用组件和配置界面，
请从[用户手册](../README.md)开始。

- [源码构建与打包](build.md)：依赖、绑定生成和 CI。
- [文字管线](text-pipeline.md)：更新调度、布局复用、字体缓存和字形网格。
- [文字管线验证](text-pipeline-validation.md)：Player 基准和图像检查。
- [自定义富文本解析器](rich-text-parsers.md)：注册解析器和构造段落载荷。
- [HybridInput](hybrid-input.md)：提供者选择和输入所有权。
- [TextInput 无障碍设计](accessibility-design.md)：平台模型与 Unity 集成。

托管代码和原生插件必须来自同一修订。修改 C API 后，通过 `./gradlew h2cs`
重新生成绑定，不要手动修改生成文件。
