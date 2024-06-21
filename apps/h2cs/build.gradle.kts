plugins {
    kotlin("jvm") version "1.9.22"
    antlr
    `java-library`
    application
    id("com.github.johnrengelman.shadow") version "8.1.1"
}

group = "com.morizero"
version = "1.0-SNAPSHOT"

repositories {
    mavenCentral()
}

dependencies {
    testImplementation("org.jetbrains.kotlin:kotlin-test")
    antlr("org.antlr:antlr4:4.13.1")
}

tasks.generateGrammarSource {
    maxHeapSize = "64m"
    arguments = arguments + listOf("-visitor", "-long-messages")
    outputDirectory = outputDirectory.resolve("com/morizero/h2cs/generated/parser")
}

tasks.compileKotlin {
    dependsOn(tasks.generateGrammarSource)
}

tasks.compileJava {
    dependsOn(tasks.generateGrammarSource)
}

tasks.test {
    useJUnitPlatform()
}

application {
    mainClass = "com.morizero.h2cs.Main"
}

tasks.shadowJar {

}

kotlin {
    jvmToolchain(11)
}
