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
val h2csCsharpBindingOutputFile = file("apps/unity-plugins/Milestro/Binding/BindingC.cs")
val h2csCppFrameworkBindingOutputFile = file("apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp")
val unityPluginsFormatSolutionFile = file("apps/unity-plugins/Milestro.UnityPlugins.Format.slnx")
val unityPluginSourceDir = file("apps/unity-plugins")
val unityPluginPackageOutputDir = providers.gradleProperty("milestroUnityPluginOutputDir")
    .map { file(it) }
    .orElse(layout.buildDirectory.dir("unity-plugin").map { it.asFile })
val icuDataFile = file("ext/icu-cmake/common/icudtl.dat")

tasks {
    val syncUnityPluginMilestro = register<Sync>("syncUnityPluginMilestro") {
        group = "build"
        description = "Copies the Milestro Unity runtime assembly sources into the package output."

        from(unityPluginSourceDir.resolve("Milestro"))
        into(unityPluginPackageOutputDir.map { it.resolve("Milestro") })
    }

    val syncUnityPluginEditor = register<Sync>("syncUnityPluginEditor") {
        group = "build"
        description = "Copies the Milestro Unity editor assembly sources into the package output."

        from(unityPluginSourceDir.resolve("Milestro.Editor"))
        into(unityPluginPackageOutputDir.map { it.resolve("Milestro.Editor") })
    }

    val syncUnityPluginExperimental = register<Sync>("syncUnityPluginExperimental") {
        group = "build"
        description = "Copies the Milestro Unity experimental assembly sources into the package output."

        from(unityPluginSourceDir.resolve("Milestro.Experimental"))
        into(unityPluginPackageOutputDir.map { it.resolve("Milestro.Experimental") })
    }

    val syncUnityPluginResources = register<Sync>("syncUnityPluginResources") {
        group = "build"
        description = "Copies Milestro Unity resources and generates the ICU TextAsset."
        duplicatesStrategy = DuplicatesStrategy.FAIL

        from(unityPluginSourceDir.resolve("Resources"))
        into(unityPluginPackageOutputDir.map { it.resolve("Resources") })
        from(icuDataFile) {
            into("Milestro")
            rename { "icudtl.dat.bytes" }
        }
    }

    register("packageUnityPlugin") {
        group = "build"
        description = "Packages Milestro Unity plugin sources and resources."
        dependsOn(
            syncUnityPluginMilestro,
            syncUnityPluginEditor,
            syncUnityPluginExperimental,
            syncUnityPluginResources,
        )

        doLast {
            logger.lifecycle("Packaged Milestro Unity plugin to ${unityPluginPackageOutputDir.get()}.")
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
