plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.ndi.app"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.ndi.app"
        minSdk = 24
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"

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
}

kotlin {
    jvmToolchain(21)
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
