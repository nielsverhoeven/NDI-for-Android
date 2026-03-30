import java.util.Properties

plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
}

fun localProperty(name: String): String {
    val propertiesFile = rootProject.file("local.properties")
    if (!propertiesFile.exists()) {
        return ""
    }

    val properties = Properties().apply {
        propertiesFile.inputStream().use(::load)
    }
    return properties.getProperty(name, "")
}

val resolvedNdiSdkDir: String = localProperty("ndi.sdk.dir")
    .ifBlank { System.getenv("NDI_SDK_DIR") ?: "" }
    .ifBlank {
        val defaultPath = "C:/Program Files/NDI/NDI 6 SDK (Android)"
        if (file(defaultPath).exists()) defaultPath else ""
    }

android {
    namespace = "com.ndi.sdkbridge"
    compileSdk = 34

    defaultConfig {
        minSdk = 24
        externalNativeBuild {
            cmake {
                cppFlags += "-std=c++17"
                arguments += listOf(
                    "-DNDI_SDK_DIR=$resolvedNdiSdkDir",
                )
                abiFilters += listOf("arm64-v8a", "armeabi-v7a")
            }
        }
    }

    buildTypes {
        release {
            consumerProguardFiles("consumer-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    externalNativeBuild {
        cmake {
            path = file("src/main/cpp/CMakeLists.txt")
        }
    }

    sourceSets {
        getByName("main") {
            jniLibs.srcDirs("src/main/jniLibs")
        }
    }
}

kotlin {
    jvmToolchain(21)
}

dependencies {
    implementation(project(":core:model"))
    implementation(libs.kotlinx.coroutines.core)
    implementation(libs.jmdns)
    testImplementation(libs.junit4)
}

val syncNdiRuntimeLibs = tasks.register("syncNdiRuntimeLibs") {
    doLast {
        if (resolvedNdiSdkDir.isBlank()) {
            logger.warn("NDI SDK directory not configured; skipping libndi.so sync")
            return@doLast
        }

        val sdkLibRoot = file("$resolvedNdiSdkDir/Lib")
        val destinationRoot = file("src/main/jniLibs")
        val supportedAbis = listOf("arm64-v8a", "armeabi-v7a")

        supportedAbis.forEach { abi ->
            val sourceLib = file("${sdkLibRoot.path}/$abi/libndi.so")
            if (!sourceLib.exists()) {
                logger.warn("Missing NDI runtime for $abi at ${sourceLib.path}")
                return@forEach
            }
            val targetDir = file("${destinationRoot.path}/$abi").apply { mkdirs() }
            sourceLib.copyTo(file("${targetDir.path}/libndi.so"), overwrite = true)
        }
    }
}

tasks.named("preBuild") {
    dependsOn(syncNdiRuntimeLibs)
}
