# 文字管线验证

[English](../../en/development/text-pipeline-validation.md) | [简体中文](text-pipeline-validation.md)

本页供维护者运行基准和检查图像，不是组件使用教程。
如果只是启用或配置渲染方式，请查阅[渲染参考](../reference/rendering.md)。
当前实现和验收边界见[文字管线](text-pipeline.md)。

## 构建和运行

Unity 验证工程需要导入 `apps/unity-plugins/` 下的 `Milestro`、`Milestro.Tests/Support`、
`Milestro.Tests/EditorMode` 及其依赖和 `Resources`，并放入同一修订的原生插件与 ICU 数据。
必须使用有图形设备的主机，不要给 Player 传入 `-nographics`。

从仓库根目录运行：

```sh
"<unity-editor>" -batchmode -quit -projectPath "<unity-project>" \
  -executeMethod Milestro.Tests.TextPipelineBenchmarkBuilder.Build \
  -logFile "build/text-pipeline-player-build.log"
```

builder 在工程内生成 `Build/TextPipelineBenchmark/TextPipelineBenchmark.exe`，
采用 Mono Development 配置，并启用帧计时统计。
可通过环境变量 `MILESTRO_TEXT_BENCHMARK_PLAYER` 指定输出位置。

先执行短检查：

```sh
"<unity-project>/Build/TextPipelineBenchmark/TextPipelineBenchmark.exe" \
  -force-d3d11 -screen-fullscreen 1 -window-mode borderless \
  -screen-width 1920 -screen-height 1080 \
  --text-counts=9 --text-warmup=16 --text-samples=16 --text-repeats=1 \
  --text-output="build/text-pipeline-smoke.json" \
  -logFile "build/text-pipeline-smoke.log"
```

报告路径相对于进程工作目录。也可设置 `MILESTRO_TEXT_BENCHMARK_REPORT`；
未指定时写入 Unity 持久化数据目录。短检查成功只证明能执行，不证明性能改善。

## 负载和选项

默认运行 Paragraph 和 SlimText，标签数为 9、100、500，预热 120 帧、采样 240 帧、重复两次。
每次按 texture、mesh、mesh、texture 顺序运行。标签固定为 132 × 28，字号为 16；
内容是固定种子的八字符序列。500 标签需要足够的实际分辨率。

| 选项 | 用途 |
| --- | --- |
| `--text-counts=9,100`、`--text-kinds=paragraph,slim` | 选择数量和组件 |
| `--text-warmup=120`、`--text-samples=240`、`--text-repeats=2` | 固定预热、采样和顺序重复次数 |
| `--text-font-mappings` | 配置 body/heading 映射，并让 Paragraph 请求 body；两版本必须一致 |
| `--text-attribution` | 独立记录阶段归因，不与主计时混用 |
| `--text-freshness`、`--text-completion-timing` | 分别运行逐帧或边界完成状态观察 |
| `--text-focus-diagnostics` | 诊断运行中增加内部焦点观察 |
| `--text-diagnostic-trace` | 单独记录动态 9 标签 Paragraph 的逐标签状态 |
| `--text-request-interval-ms=4` | trace 中使用绝对时间节奏；零表示不限速，支持有限的 0–100 ms |
| `--text-start-signal=<signal-file>` | trace 在启动时等待外部文件，最多 60 秒 |
| `--text-measurement-fault=never-completes` | 仅用于独立 freshness 故障验证，不能进入性能统计 |

完整选项和报告字段见[英文详细参考](../../en/development/text-pipeline-validation.md)。

## 如何判断结果

跨修订对比需要分别保留 baseline 和 candidate Player，并使用各自匹配的托管与原生文件。
同一个 Player 内的 texture/mesh 顺序不能替代版本对照。实际 DLL 哈希应独立核实，
不能只相信 `--text-native-sha256` 等调用者提供的声明。

核对报告里的实际分辨率、输入族、语料校验值及有效样本数。主计时、归因、完成观察和 trace
分别运行，因为探针会增加成本。预热会重放完整语料，以覆盖字形与相位组合；共享图集保留在案例之间，
所以这不是独立冷启动测量。

完成观察分请求和 drain 两阶段。停止 setter 后，正常调度至少推进一帧，最多 120 帧或 5000 ms。
达到时间上限优先失败，即使当次观察已收敛；第 120 帧只能在时间上限之前成功。
失败必须体现在报告和非零退出码中。协程无法终止卡死的主线程，因此外部还需要进程超时。

最终必须有有效输出、最新内容已完成，且 producer 输出版本与 presenter 观察版本一致。
内容版本和输出版本是两套序列，不能互相比较。完成指纹理回调或同步网格发布，不是 GPU 显示。
正常合并会丢弃中间请求，因此最终收敛不代表历史请求全部完成。
吞吐量的时间分母包含 drain，比较时也要保留实际请求量和完成量。

启动前台门依赖每次运行独立的新 signal 文件，外部应保留释放时间并另设进程超时。
它不替代 request/drain 期间的焦点检查。外部观察应按校准后的 QPC/Stopwatch 时间关联，
API 失败视为未知；内部焦点不能覆盖外部反证。保存所有失败和原始帧，不因数字不好重跑或删帧。
仓库没有附带完整的外部观察器和分析脚本。

固定节奏 trace 改变了负载，不能用来豁免原完成率预算。它保留全部等待、请求和 drain 观察，
但只能对事件时间给出观察区间，不能提供精确回调时间。容量耗尽时失败并保留部分 trace。
没有有效 GPU 样本时，不报告 GPU 收益。Unity 和托管内存计数也不能证明原生缓存长期有界。

## 图像检查

在有图形设备的主机运行 PlayMode `TextGlyphMeshImageComparisonTests`。
环境变量 `MILESTRO_TEXT_IMAGE_OUTPUT` 指定输出目录，`MILESTRO_TEXT_IMAGE_FONT` 可指定字体。
默认使用仓库测试字体，每案输出 baseline、candidate、difference 图像和 summary。

保留失败图像和原阈值。测试覆盖部分整数、小数位置、Canvas 缩放、硬边裁剪和多行场景，
不能外推到所有平台或复杂文字。Linear 与非白源色当前会回退到纹理；回退图像通过不代表
这些场景的字形网格画质通过。
