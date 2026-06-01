plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.jvm) apply false
    alias(libs.plugins.ksp) apply false
}

tasks.register("buildNdiSdkBridgeRelease") {
    group = "verification"
    description = "Builds ndi/sdk-bridge release APK required by dual-emulator e2e setup"
    dependsOn(":ndi:sdk-bridge:assembleRelease")
}
