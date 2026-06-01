import java.util.Properties
import java.io.File

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

private data class VersionState(
    val code: Int,
    val name: String,
    val lastFeatureKey: String?,
)

// Helper function to read version properties
private fun readVersionProperties(): VersionState {
    val versionPropsFile = rootProject.file("version.properties")
    val versionProps = Properties()
    if (versionPropsFile.exists()) {
        versionPropsFile.inputStream().use { versionProps.load(it) }
    }
    val code = (versionProps.getProperty("versionCode", "1") as String).toInt()
    val name = versionProps.getProperty("versionName", "0.1.0") as String
    val lastFeatureKey = versionProps.getProperty("lastFeatureKey")
    return VersionState(code = code, name = name, lastFeatureKey = lastFeatureKey)
}

private fun readCurrentGitBranchOrNull(): String? {
    return runCatching {
        val process = ProcessBuilder("git", "rev-parse", "--abbrev-ref", "HEAD")
            .directory(rootProject.rootDir)
            .redirectErrorStream(true)
            .start()
        val output = process.inputStream.bufferedReader().use { it.readText().trim() }
        val exitCode = process.waitFor()
        if (exitCode == 0) output else null
    }.getOrNull()
}

private fun extractFeatureKeyOrNull(branchName: String?): String? {
    if (branchName.isNullOrBlank()) {
        return null
    }

    val featureMatch = Regex("^(\\d+)-").find(branchName)
    return featureMatch?.groupValues?.getOrNull(1)
}

private fun parseSemVerOrDefault(versionName: String): Triple<Int, Int, Int> {
    val parts = versionName.split('.')
    if (parts.size != 3) {
        return Triple(0, 1, 0)
    }

    val major = parts[0].toIntOrNull() ?: 0
    val minor = parts[1].toIntOrNull() ?: 1
    val patch = parts[2].toIntOrNull() ?: 0
    return Triple(major, minor, patch)
}

// Helper function to increment and write version
fun incrementAndWriteVersion(): Pair<Int, String> {
    val versionPropsFile = rootProject.file("version.properties")
    val current = readVersionProperties()
    val newCode = current.code + 1

    val (major, minor, patch) = parseSemVerOrDefault(current.name)
    val currentFeatureKey = extractFeatureKeyOrNull(readCurrentGitBranchOrNull())
    val shouldBumpMinor = !currentFeatureKey.isNullOrBlank() && currentFeatureKey != current.lastFeatureKey

    val newName = if (shouldBumpMinor) {
        "${major}.${minor + 1}.0"
    } else {
        "${major}.${minor}.${patch + 1}"
    }

    val newProps = Properties()
    newProps.setProperty("versionCode", newCode.toString())
    newProps.setProperty("versionName", newName)
    if (!currentFeatureKey.isNullOrBlank()) {
        newProps.setProperty("lastFeatureKey", currentFeatureKey)
    } else if (!current.lastFeatureKey.isNullOrBlank()) {
        newProps.setProperty("lastFeatureKey", current.lastFeatureKey)
    }

    versionPropsFile.outputStream().use { newProps.store(it, "Version incremented before build") }

    val bumpReason = if (shouldBumpMinor) {
        "(feature bump: ${current.lastFeatureKey ?: "none"} -> $currentFeatureKey)"
    } else {
        "(patch bump)"
    }
    println("  ✓ Version incremented: versionCode ${current.code} → $newCode, versionName ${current.name} → $newName $bumpReason")
    return Pair(newCode, newName)
}

// Increment version at the START of each build
val (appVersionCode, appVersionName) = incrementAndWriteVersion()

val releaseKeystoreFile = System.getenv("RELEASE_KEYSTORE_FILE")
val releaseKeystorePassword = System.getenv("RELEASE_KEYSTORE_PASSWORD")
val releaseKeyAlias = System.getenv("RELEASE_KEY_ALIAS")
val releaseKeyPassword = System.getenv("RELEASE_KEY_PASSWORD")

val hasReleaseSigningConfig =
    !releaseKeystoreFile.isNullOrBlank() &&
        !releaseKeystorePassword.isNullOrBlank() &&
        !releaseKeyAlias.isNullOrBlank() &&
        !releaseKeyPassword.isNullOrBlank() &&
        File(releaseKeystoreFile).exists()

if (hasReleaseSigningConfig) {
    println("  ✓ Release signing configuration detected.")
} else {
    println("  ! Release signing configuration not detected. Release builds will use fallback signing.")
}

android {
    namespace = "com.ndi.app"
    signingConfigs {
        if (hasReleaseSigningConfig) {
            create("release") {
                storeFile = File(releaseKeystoreFile!!)
                storePassword = releaseKeystorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }
    compileSdk = 34

    defaultConfig {
        applicationId = "com.ndi.app"
        minSdk = 24
        targetSdk = 34
        versionCode = appVersionCode
        versionName = appVersionName

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
            if (hasReleaseSigningConfig) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        buildConfig = true
        viewBinding = true
    }

    lint {
        baseline = file("lint-baseline.xml")
    }
}

kotlin {
    jvmToolchain(21)
}

afterEvaluate {
    val androidExtension = project.extensions.getByType<com.android.build.api.dsl.ApplicationExtension>()
    val versionName = androidExtension.defaultConfig.versionName ?: "dev"
    val exportDir = rootProject.file("exports")

    val renameDebugApk = tasks.register("renameDebugApk") {
        doLast {
            val apkDir = layout.buildDirectory.dir("outputs/apk/debug").get().asFile
            val originalApk = File(apkDir, "app-debug.apk")
            val renamedApk = File(apkDir, "ndi-for-android-${versionName}.apk")
            if (originalApk.exists()) {
                originalApk.renameTo(renamedApk)
            }
            
            // Copy to export folder
            if (renamedApk.exists()) {
                exportDir.mkdirs()
                val exportedApk = File(exportDir, renamedApk.name)
                renamedApk.copyTo(exportedApk, overwrite = true)
                println("  ✓ APK exported to ${exportedApk.absolutePath}")
            }
        }
    }

    val renameReleaseApk = tasks.register("renameReleaseApk") {
        doLast {
            val apkDir = layout.buildDirectory.dir("outputs/apk/release").get().asFile
            val originalApk = File(apkDir, "app-release.apk")
            val renamedApk = File(apkDir, "ndi-for-android-${versionName}.apk")
            if (originalApk.exists()) {
                originalApk.renameTo(renamedApk)
            }
            
            // Copy to export folder
            if (renamedApk.exists()) {
                exportDir.mkdirs()
                val exportedApk = File(exportDir, renamedApk.name)
                renamedApk.copyTo(exportedApk, overwrite = true)
                println("  ✓ APK exported to ${exportedApk.absolutePath}")
            }
        }
    }

    tasks.named("assembleDebug") {
        finalizedBy(renameDebugApk)
    }

    tasks.named("assembleRelease") {
        finalizedBy(renameReleaseApk)
    }
}

tasks.register("verifyReleaseHardening") {
    group = "verification"
    description = "Ensures release minification and resource shrinking remain enabled."

    doLast {
        val release = project.extensions.getByType<com.android.build.api.dsl.ApplicationExtension>().buildTypes.getByName("release")
        check(release.isMinifyEnabled) { "Release minification must stay enabled." }
        check(release.isShrinkResources) { "Release resource shrinking must stay enabled." }
    }
}

dependencies {
    implementation(project(":core:model"))
    implementation(project(":core:database"))
    implementation(project(":feature:ndi-browser:domain"))
    implementation(project(":feature:ndi-browser:data"))
    implementation(project(":feature:ndi-browser:presentation"))
    implementation(project(":feature:theme-editor:domain"))
    implementation(project(":feature:theme-editor:data"))
    implementation(project(":feature:theme-editor:presentation"))
    implementation(project(":ndi:sdk-bridge"))

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.activity.ktx)
    implementation(libs.androidx.fragment.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.navigation.fragment.ktx)
    implementation(libs.androidx.navigation.ui.ktx)
    implementation(libs.google.material)

    testImplementation(libs.junit4)
    testImplementation(libs.kotlinx.coroutines.test)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.espresso.core)
}
