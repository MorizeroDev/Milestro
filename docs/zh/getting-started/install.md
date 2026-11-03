# 安装到 Unity 项目

[English](../../en/getting-started/install.md) | [简体中文](install.md)

预编译 Unity 包包含托管 C# 源码、原生插件和所需资源，面向 Unity 6。
使用预编译包时，无需自行构建或生成绑定。

## 1. 下载

- **GitHub Releases**：下载 `milestro-unity-plugins.tar.gz`。
- **GitHub Actions**：打开 **Actions → Formal Build and Release → 对应运行 → Artifacts**，
  下载 `milestro-unity-plugins`。Actions 下载的是 ZIP，先解压一次，取得内部的 `.tar.gz`。

## 2. 解压到 Assets

在 Unity 项目根目录执行：

```sh
tar -xzf milestro-unity-plugins.tar.gz -C Assets
```

默认会得到 `Assets/unity-plugins/`。也可用归档工具导入其中所有内容；中间目录名称与层级可以变化，
但目录结构及 `.meta` 必须保留。

完整导入 `Milestro/`、`Milestro.Editor/`、`Milestro.Experimental/`、`Milestro.InputSystem/`、
`Milestro.RichText.Markdown/`、`Milestro.RichText.TMPro/`
和 `Resources/`。不要改掉 `Resources` 目录名，Unity 将 `Assets/` 下任意深度的同名目录视为资源根。
其中的 `Resources/Milestro/icudtl.dat.bytes` 用于初始化 ICU。

Markdown 模块包含 `Plugins/Markdig.dll` 和 `license.txt`。使用此模块时，将
**Player Settings → Other Settings → API Compatibility Level** 设为 **.NET Standard 2.1**。
解析器选择见[富文本](../guides/text.md#rich-text)。

## 3. 添加必需包

将以下依赖合并进现有 `Packages/manifest.json` 的 `dependencies`，不要覆盖项目原有依赖：

```json
{
  "dependencies": {
    "party.para.util.colors": "https://github.com/ParaParty/ParaPartyUtil.git?path=Colors",
    "party.para.util.unitynative": "https://github.com/ParaParty/ParaPartyUtil.git?path=UnityNative"
  }
}
```

等待 Package Manager 解析完成。这两个包是 Milestro 程序集的必需依赖。

## 4. 运行与升级

继续阅读 [第一段文本](first-text.md)。可编辑 `TextInput` 还要求版本范围
`[1.16.0,2.0.0-0)` 的 `com.unity.inputsystem` 和 `InputSystemUIInputModule`，
见 [第一个输入框](first-input.md)。

归档包含 Windows x86-64、Linux x86-64、macOS arm64、Android arm64-v8a、iOS arm64 原生库。
不能加载原生库时，见 [排查问题](troubleshoot.md)。

升级时，从同一发布版本或源码修订一起替换托管源码、资源与原生插件。
清理旧版本遗留的 Milestro 文件，避免重复程序集和过期插件；应用自己的字体、脚本和 prefab 应单独保存。
