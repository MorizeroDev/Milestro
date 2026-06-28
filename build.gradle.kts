plugins {
    id("party.para.h2cs") version "1.0.2" apply true
}

group = "com.morizero.milestro"
version = "0.0.1"

subprojects {
    group = "com.morizero.milestro"
    version = "0.0.1"
    repositories { repo() }
}

repositories { repo() }

tasks {
    h2cs {
        projectName = "Milestro"
        sourceFilePath = file("include/Milestro/game/milestro_game_interface.h").absolutePath
        csharpBindingOutputPath = file("apps/unity-plugins/Milestro/Binding/BindingC.cs").absolutePath
        cppFrameworkBindingOutputPath = file("apps/unity-plugins/Milestro/Plugins/FrameworkBinding.cpp").absolutePath
        addTypeMapping(
            listOf("milestro::skia::Canvas") to "IntPtr",
            listOf("milestro::skia::Image") to "IntPtr",
            listOf("milestro::skia::Typeface") to "IntPtr",
            listOf("milestro::skia::Font") to "IntPtr",
            listOf("milestro::skia::Path") to "IntPtr",
            listOf("milestro::skia::Svg") to "IntPtr",
            listOf("milestro::skia::VertexData") to "IntPtr",
            listOf("milestro::skia::textlayout::Paragraph") to "IntPtr",
            listOf("milestro::skia::textlayout::ParagraphBuilder") to "IntPtr",
            listOf("milestro::skia::textlayout::ParagraphStyle") to "IntPtr",
            listOf("milestro::skia::textlayout::StrutStyle") to "IntPtr",
            listOf("milestro::skia::textlayout::TextStyle") to "IntPtr",

            listOf("milestro::icu::IcuUCollator") to "IntPtr",
        )
    }
}

fun RepositoryHandler.repo() {
    mavenLocal()
    mavenCentral()
    maven { url = uri("https://plugins.gradle.org/m2/") }
    maven { url = uri("https://maven.para.party") }
}
