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

android {
    namespace = "com.ndi.sdkbridge"
    compileSdk = 34

    defaultConfig {
        minSdk = 24
        externalNativeBuild {
            cmake {
                cppFlags += "-std=c++17"
                arguments += listOf(
                    "-DNDI_SDK_DIR=${localProperty("ndi.sdk.dir")}",
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
