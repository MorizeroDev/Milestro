
apply(plugin = "java")
apply(plugin = "java-library")
apply(plugin = "maven-publish")
apply(plugin = "signing")

fun org.gradle.api.Project.`java`(configure: Action<org.gradle.api.plugins.JavaPluginExtension>): Unit =
    (this as org.gradle.api.plugins.ExtensionAware).extensions.configure("java", configure)

val org.gradle.api.Project.`publishing`: org.gradle.api.publish.PublishingExtension
    get() = (this as org.gradle.api.plugins.ExtensionAware).extensions.getByName("publishing") as org.gradle.api.publish.PublishingExtension

fun org.gradle.api.Project.`publishing`(configure: Action<org.gradle.api.publish.PublishingExtension>): Unit =
    (this as org.gradle.api.plugins.ExtensionAware).extensions.configure("publishing", configure)

val org.gradle.api.Project.`ext`: org.gradle.api.plugins.ExtraPropertiesExtension
    get() = (this as org.gradle.api.plugins.ExtensionAware).extensions.getByName("ext") as org.gradle.api.plugins.ExtraPropertiesExtension

fun org.gradle.api.Project.`signing`(configure: Action<org.gradle.plugins.signing.SigningExtension>): Unit =
    (this as org.gradle.api.plugins.ExtensionAware).extensions.configure("signing", configure)

val TaskContainer.`javadoc`: TaskProvider<org.gradle.api.tasks.javadoc.Javadoc>
    get() = named<org.gradle.api.tasks.javadoc.Javadoc>("javadoc")

val selfProject = project

selfProject.java {
    withJavadocJar()
    withSourcesJar()
}

selfProject.publishing {
    publications {
        publications.withType<MavenPublication> {
            groupId = selfProject.group.toString()
            version = selfProject.version.toString()
            artifactId = selfProject.ext.properties["ARTIFACT_ID"]?.toString() ?: selfProject.name

//            from(components.getByName("java"))
//            artifacts {
//                archives("javadocJar")
//                archives("sourcesJar")
//            }

            versionMapping {
                usage("java-api") {
                    fromResolutionOf("runtimeClasspath")
                }
                usage("java-runtime") {
                    fromResolutionResult()
                }
            }
            pom {
                name.set(selfProject.ext.properties["POM_NAME"]?.toString() ?: selfProject.name)
                description.set(selfProject.ext.properties["POM_DESCRIPTION"].toString() ?: selfProject.name)
                url.set("https://pkg.para.party/H2CS")
                developers {
                    developer {
                        id.set("ericlian")
                        name.set("Eric_Lian")
                        email.set("public@superexercisebook.com")
                    }
                }
                scm {
                    connection.set("scm:git:https://github.com/ParaParty/H2CS.git")
                    developerConnection.set("scm:git:https://github.com/ParaParty/H2CS.git")
                    url.set("https://github.com/ParaParty/H2CS")
                }
            }
        }
    }
    repositories {
        maven {
            // change URLs to point to your repos, e.g. http://my.org/repo
            val releasesRepoUrl = uri(layout.buildDirectory.dir("repos/releases"))
            val snapshotsRepoUrl = uri(layout.buildDirectory.dir("repos/snapshots"))
//            val releasesRepoUrl = uri("https://s01.oss.sonatype.org/service/local/staging/deploy/maven2/")
//            val snapshotsRepoUrl = uri("https://s01.oss.sonatype.org/content/repositories/snapshots/")
//            credentials {
//                username = (selfProject.findProperty("ossrhUsername") ?: System.getenv("OSSRH_USERNAME")).toString()
//                password = (selfProject.findProperty("ossrhPassword") ?: System.getenv("OSSRH_PASSWORD")).toString()
//            }

            url = if (version.toString().endsWith("SNAPSHOT")) snapshotsRepoUrl else releasesRepoUrl
        }
    }
}

//selfProject.signing {
//    sign(selfProject.publishing.publications)
//}

selfProject.tasks.javadoc {
    if (JavaVersion.current().isJava9Compatible) {
        (options as StandardJavadocDocletOptions).addBooleanOption("html5", true)
    }
}
