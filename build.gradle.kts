import java.io.File
import java.nio.file.Files
import java.nio.file.LinkOption
import java.nio.file.StandardOpenOption

plugins {
    id("party.para.h2cs") version "2.0.0" apply true
}

group = "com.morizero.milestro"
version = "0.0.1"

subprojects {
    group = "com.morizero.milestro"
    version = "0.0.1"
    repositories { repo() }
}

repositories { repo() }

val h2csSourceFile = file("include/Milestro/game/milestro_game_interface.h")
val h2csCsharpBindingOutputFile = providers.gradleProperty("milestroH2csCsharpBindingOutputPath")
    .map { file(it) }
    .getOrElse(file("apps/unity-plugins/Milestro/Binding/BindingC.cs"))
val h2csCppFrameworkBindingOutputFile = providers.gradleProperty("milestroH2csCppFrameworkBindingOutputPath")
    .map { file(it) }
    .getOrElse(file("apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp"))
val unityPluginsFormatSolutionFile = file("apps/unity-plugins/Milestro.UnityPlugins.Format.slnx")
val unityPluginSourceDir = file("apps/unity-plugins")
val unityPluginPackageOutputDir = providers.gradleProperty("milestroUnityPluginOutputDir")
    .map { file(it) }
    .orElse(layout.buildDirectory.dir("unity-plugin").map { it.asFile })
val normalizedUnityPluginPackageOutputDir = unityPluginPackageOutputDir.map {
    it.toPath().toAbsolutePath().normalize().toFile()
}
val canonicalUnityPluginPackageOutputDir = normalizedUnityPluginPackageOutputDir.map { it.canonicalFile }
val icuDataFile = file("ext/icu-cmake/common/icudtl.dat")
val unityPackageSourceRoots = listOf(
    "Milestro",
    "Milestro.Editor",
    "Milestro.Experimental",
    "Milestro.InputSystem",
    "Resources",
)
val unityPackageSourceEntries = unityPackageSourceRoots + unityPackageSourceRoots
    .map { "$it.meta" }
    .filter { unityPluginSourceDir.resolve(it).isFile }
val unityPackageSourceIncludes = unityPackageSourceEntries.map { entry ->
    if (unityPluginSourceDir.resolve(entry).isDirectory) "$entry/**" else entry
}
val unityPackageSourceExcludes = listOf("**/.DS_Store")
val unityPackageSourceFiles = fileTree(unityPluginSourceDir) {
    include(unityPackageSourceIncludes)
    exclude(unityPackageSourceExcludes)
}
val generatedUnityPackageFiles = setOf("Resources/Milestro/icudtl.dat.bytes")

fun unityPackageOutputSentinel(outputDir: File) =
    outputDir.parentFile.resolve(".${outputDir.name}.milestro-unity-package-output")

fun unityPackageOutputSentinelContents(projectDir: File, outputDir: File) =
    "milestro-unity-package-output-v1\nproject=${projectDir.path}\noutput=${outputDir.path}\n"

fun unityPackageOutputSentinelIsValid(sentinel: File, expectedContents: String): Boolean {
    val path = sentinel.toPath()
    return Files.isRegularFile(path, LinkOption.NOFOLLOW_LINKS) &&
        Files.size(path) <= 4096L &&
        Files.readString(path) == expectedContents
}

tasks {
    val copyUnityPluginRuntime = register<Copy>("copyUnityPluginRuntime") {
        group = "build"
        description = "Copies the explicitly allowlisted Milestro Unity package roots into the package output."
        duplicatesStrategy = DuplicatesStrategy.FAIL
        doNotTrackState("The validated Unity package output is completely rebuilt on every run.")

        doFirst {
            val configuredOutputDir = normalizedUnityPluginPackageOutputDir.get()
            val outputDir = canonicalUnityPluginPackageOutputDir.get()
            val outputPath = outputDir.toPath()
            val projectPath = projectDir.canonicalFile.toPath()
            val sourcePath = unityPluginSourceDir.canonicalFile.toPath()
            var unsafeReason = when {
                Files.isSymbolicLink(configuredOutputDir.toPath()) ->
                    "the configured output path is a symbolic link"
                outputDir.parentFile == null -> "it is a filesystem root"
                projectPath.startsWith(outputPath) -> "it contains the project directory"
                sourcePath.startsWith(outputPath) -> "it contains the Unity package source directory"
                outputPath.startsWith(sourcePath) -> "it is inside the Unity package source directory"
                outputDir.exists() && !outputDir.isDirectory -> "the output exists but is not a directory"
                else -> null
            }
            if (unsafeReason == null) {
                val sentinel = unityPackageOutputSentinel(outputDir)
                val expectedSentinel = unityPackageOutputSentinelContents(projectDir.canonicalFile, outputDir)
                val sentinelExists = Files.exists(sentinel.toPath(), LinkOption.NOFOLLOW_LINKS)
                val sentinelIsValid = unityPackageOutputSentinelIsValid(sentinel, expectedSentinel)
                val outputEntries = outputDir.listFiles()
                unsafeReason = when {
                    sentinelExists && !sentinelIsValid ->
                        "the ownership sentinel does not match this project and destination"
                    outputDir.isDirectory && outputEntries == null ->
                        "the output directory cannot be inspected"
                    outputEntries?.isNotEmpty() == true && !sentinelIsValid ->
                        "the non-empty output is not owned by this task"
                    else -> null
                }
            }
            if (unsafeReason != null) {
                throw GradleException(
                    "Unsafe Unity package output directory " +
                        "(configured '$configuredOutputDir', canonical '$outputDir'): $unsafeReason.",
                )
            }
            if (outputDir.exists() && !outputDir.deleteRecursively()) {
                throw GradleException(
                    "Could not rebuild Unity package output directory " +
                        "(configured '$configuredOutputDir', canonical '$outputDir').",
                )
            }
        }
        from(unityPackageSourceFiles)
        from(icuDataFile) {
            into("Resources/Milestro")
            rename { "icudtl.dat.bytes" }
        }
        into(canonicalUnityPluginPackageOutputDir)

        doLast {
            val configuredOutputDir = normalizedUnityPluginPackageOutputDir.get()
            val outputDir = canonicalUnityPluginPackageOutputDir.get()
            val sentinel = unityPackageOutputSentinel(outputDir)
            val expectedSentinel = unityPackageOutputSentinelContents(projectDir.canonicalFile, outputDir)
            val sentinelPath = sentinel.toPath()
            if (Files.exists(sentinelPath, LinkOption.NOFOLLOW_LINKS)) {
                if (!unityPackageOutputSentinelIsValid(sentinel, expectedSentinel)) {
                    throw GradleException(
                        "Unity package output sentinel changed during packaging " +
                            "(configured '$configuredOutputDir', canonical '$outputDir'): ${sentinel.path}",
                    )
                }
            } else {
                Files.writeString(
                    sentinelPath,
                    expectedSentinel,
                    StandardOpenOption.CREATE_NEW,
                    StandardOpenOption.WRITE,
                )
            }
        }
    }

    val verifyUnityPluginPackage = register("verifyUnityPluginPackage") {
        group = "verification"
        description = "Verifies that the Unity package exactly matches the release-root allowlist."
        dependsOn(copyUnityPluginRuntime)

        doLast {
            unityPackageSourceEntries.forEach { entry ->
                val source = unityPluginSourceDir.resolve(entry)
                if (!source.exists()) {
                    throw GradleException("Unity package allowlist source is missing: $entry")
                }
            }
            val expectedFiles = unityPackageSourceFiles.files
                .asSequence()
                .filter { it.isFile }
                .map { it.relativeTo(unityPluginSourceDir).invariantSeparatorsPath }
                .toMutableSet()
            expectedFiles.addAll(generatedUnityPackageFiles)

            val outputDir = canonicalUnityPluginPackageOutputDir.get()
            val actualFiles = if (outputDir.isDirectory) {
                outputDir.walkTopDown()
                    .filter { it.isFile }
                    .map { it.relativeTo(outputDir).invariantSeparatorsPath }
                    .toSet()
            } else {
                emptySet()
            }
            val missingFiles = expectedFiles - actualFiles
            val unexpectedFiles = actualFiles - expectedFiles
            if (missingFiles.isNotEmpty() || unexpectedFiles.isNotEmpty()) {
                val details = buildString {
                    if (missingFiles.isNotEmpty()) {
                        appendLine("Missing Unity package files:")
                        missingFiles.sorted().forEach { appendLine("  $it") }
                    }
                    if (unexpectedFiles.isNotEmpty()) {
                        appendLine("Unexpected or stale Unity package files:")
                        unexpectedFiles.sorted().forEach { appendLine("  $it") }
                    }
                }.trimEnd()
                throw GradleException(details)
            }

            val guidPattern = Regex("(?m)^guid:\\s*(\\S+)\\s*$")
            val metaFiles = actualFiles.filter { it.endsWith(".meta") }.sorted()
            val metaWithoutGuid = mutableListOf<String>()
            val filesByGuid = mutableMapOf<String, MutableList<String>>()
            metaFiles.forEach { relativePath ->
                val guid = guidPattern.find(outputDir.resolve(relativePath).readText())?.groupValues?.get(1)
                if (guid == null) {
                    metaWithoutGuid.add(relativePath)
                } else {
                    filesByGuid.getOrPut(guid) { mutableListOf() }.add(relativePath)
                }
            }
            val duplicateGuids = filesByGuid.filterValues { it.size > 1 }
            if (metaWithoutGuid.isNotEmpty() || duplicateGuids.isNotEmpty()) {
                val details = buildString {
                    if (metaWithoutGuid.isNotEmpty()) {
                        appendLine("Unity package meta files without GUIDs:")
                        metaWithoutGuid.forEach { appendLine("  $it") }
                    }
                    if (duplicateGuids.isNotEmpty()) {
                        appendLine("Duplicate Unity package GUIDs:")
                        duplicateGuids.toSortedMap().forEach { (guid, files) ->
                            appendLine("  $guid")
                            files.sorted().forEach { appendLine("    $it") }
                        }
                    }
                }.trimEnd()
                throw GradleException(details)
            }

            logger.lifecycle("Verified ${actualFiles.size} allowlisted Unity package files and ${metaFiles.size} unique GUIDs.")
        }
    }

    register("packageUnityPlugin") {
        group = "build"
        description = "Packages and verifies allowlisted Milestro Unity release roots and resources."
        dependsOn(verifyUnityPluginPackage)

        doLast {
            logger.lifecycle(
                "Packaged Milestro Unity plugin " +
                    "(configured '${normalizedUnityPluginPackageOutputDir.get()}', " +
                    "canonical '${canonicalUnityPluginPackageOutputDir.get()}').",
            )
        }
    }

    register("format-cs") {
        group = "formatting"
        description = "Formats C# files under apps/unity-plugins with dotnet format."

        doLast {
            if (!hasDotnetFormat()) {
                logger.lifecycle("dotnet format not found; skipping C# formatting.")
                return@doLast
            }

            val sourceFiles = fileTree("apps/unity-plugins") {
                include("**/*.cs")
            }.files.sortedBy { it.path }

            if (sourceFiles.isEmpty()) {
                logger.lifecycle("No C# files found under apps/unity-plugins.")
                return@doLast
            }

            val exitCode = ProcessBuilder(
                "dotnet",
                "format",
                "whitespace",
                unityPluginsFormatSolutionFile.absolutePath,
                "--verbosity",
                "minimal",
            )
                .inheritIO()
                .directory(rootDir)
                .start()
                .waitFor()

            if (exitCode == 0) {
                logger.lifecycle("Formatted ${sourceFiles.size} C# files under apps/unity-plugins with dotnet format.")
            } else {
                throw GradleException("dotnet format failed for apps/unity-plugins with exit code $exitCode.")
            }
        }
    }

    register("format") {
        group = "formatting"
        description = "Formats project sources."
        dependsOn("format-cs")
    }

    h2cs {
        projectName = "Milestro"
        sourceFilePath = h2csSourceFile.absolutePath
        csharpBindingOutputPath = h2csCsharpBindingOutputFile.absolutePath
        cppFrameworkBindingOutputPath = h2csCppFrameworkBindingOutputFile.absolutePath
        addTypeMapping(
            listOf("milestro::game::model::DataEnvelop") to "IntPtr",
            listOf("milestro::game::model::BytesWrapper") to "IntPtr",
            listOf("milestro::game::model::NumberWrapper") to "IntPtr",

            listOf("milestro::unicode::StringComparator") to "IntPtr",
            listOf("milestro::unicode::Normalizer") to "IntPtr",
            listOf("milestro::unicode::Segmenter") to "IntPtr",
            listOf("milestro::unicode::Transliterator") to "IntPtr",

            listOf("milestro::skia::Canvas") to "IntPtr",
            listOf("milestro::skia::Image") to "IntPtr",
            listOf("milestro::skia::Typeface") to "IntPtr",
            listOf("milestro::skia::Font") to "IntPtr",
            listOf("milestro::skia::SlimTextDrawSnapshot") to "IntPtr",
            listOf("milestro::skia::TextDrawSnapshot") to "IntPtr",
            listOf("milestro::skia::Path") to "IntPtr",
            listOf("milestro::skia::Svg") to "IntPtr",
            listOf("milestro::skia::VertexData") to "IntPtr",
            listOf("milestro::skia::FontFamilyName") to "IntPtr",
            listOf("milestro::skia::MilestroTypefaceFamilyNameList") to "IntPtr",
            listOf("milestro::skia::MilestroFontFamilyInfo") to "IntPtr",
            listOf("milestro::skia::MilestroFontFamilyList") to "IntPtr",
            listOf("milestro::skia::MilestroFontFaceInfo") to "IntPtr",
            listOf("milestro::skia::MilestroFontFaceList") to "IntPtr",
            listOf("milestro::skia::textlayout::Paragraph") to "IntPtr",
            listOf("milestro::skia::textlayout::ParagraphBuilder") to "IntPtr",
            listOf("milestro::skia::textlayout::ParagraphStyle") to "IntPtr",
            listOf("milestro::skia::textlayout::StrutStyle") to "IntPtr",
            listOf("milestro::skia::textlayout::TextStyle") to "IntPtr",
            listOf("milestro::skia::textlayout::InputBox") to "IntPtr",
            listOf("milestro::skia::textlayout::InputBoxDrawSnapshot") to "IntPtr",

            listOf("MilestroSkiaTextlayoutParagraphSplitGlyphCallback") to
                "MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback",
        )

        doLast {
            if (!hasClangFormat()) {
                logger.lifecycle("clang-format not found; skipping generated binding formatting.")
                return@doLast
            }

            listOf(h2csCsharpBindingOutputFile, h2csCppFrameworkBindingOutputFile)
                .filter { it.isFile }
                .forEach { outputFile ->
                    val exitCode = ProcessBuilder("clang-format", "-i", outputFile.absolutePath)
                        .inheritIO()
                        .start()
                        .waitFor()

                    if (exitCode == 0) {
                        logger.lifecycle("Formatted ${outputFile.path} with clang-format.")
                    } else {
                        logger.warn("clang-format failed for ${outputFile.path} with exit code $exitCode.")
                    }
                }
        }
    }
}

fun RepositoryHandler.repo() {
    mavenLocal()
    mavenCentral()
    maven { url = uri("https://plugins.gradle.org/m2/") }
    maven { url = uri("https://maven.para.party") }
}

fun hasClangFormat(): Boolean {
    return runCatching {
        ProcessBuilder("clang-format", "--version")
            .redirectOutput(ProcessBuilder.Redirect.DISCARD)
            .redirectError(ProcessBuilder.Redirect.DISCARD)
            .start()
            .waitFor() == 0
    }.getOrDefault(false)
}

fun hasDotnetFormat(): Boolean {
    return runCatching {
        ProcessBuilder("dotnet", "format", "--version")
            .redirectOutput(ProcessBuilder.Redirect.DISCARD)
            .redirectError(ProcessBuilder.Redirect.DISCARD)
            .start()
            .waitFor() == 0
    }.getOrDefault(false)
}
