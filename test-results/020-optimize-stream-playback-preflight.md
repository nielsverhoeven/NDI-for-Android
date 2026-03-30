# Environment Preflight Results

## Date: 03/29/2026 08:25:55

### Android Prerequisites



### Dual-Emulator Preflight

{
  "operation": "verify-e2e-dual-emulator-prereqs",
  "status": "SUCCESS",
  "timestamp": "2026-03-29T06:27:03.0874348Z",
  "data": {
    "checks": [
      {
        "name": "adb",
        "ok": true
      },
      {
        "name": "emulator",
        "ok": true
      },
      {
        "name": "sdkmanager",
        "ok": true
      },
      {
        "name": "ndi-sdk-artifact",
        "ok": true,
        "path": "C:\\githubrepos\\NDI-for-Android\\ndi\\sdk-bridge\\build\\outputs\\aar\\sdk-bridge-release.aar",
        "type": "aar",
        "warning": "library-artifact-only"
      }
    ],
    "apkPath": "C:\\githubrepos\\NDI-for-Android\\ndi\\sdk-bridge\\build\\outputs\\aar\\sdk-bridge-release.aar"
  },
  "errors": [],
  "warnings": []
}


### Build Verification

Starting a Gradle Daemon (subsequent builds will be faster)

> Configure project :app
WARNING: The option setting 'android.builtInKotlin=false' is deprecated.
The current default is 'true'.
It will be removed in version 10.0 of the Android Gradle plugin.
WARNING: The option setting 'android.newDsl=false' is deprecated.
The current default is 'true'.
It will be removed in version 10.0 of the Android Gradle plugin.
  Γ£ô Version incremented: versionCode 133 ΓåÆ 134, versionName 0.7.12 ΓåÆ 0.7.13 (patch bump)

> Task :app:preBuild UP-TO-DATE
> Task :app:preDebugBuild UP-TO-DATE
> Task :app:mergeDebugNativeDebugMetadata NO-SOURCE
> Task :core:model:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :core:model:compileKotlin UP-TO-DATE
> Task :core:model:compileJava NO-SOURCE
> Task :core:model:processResources NO-SOURCE
> Task :core:model:classes UP-TO-DATE
> Task :core:model:jar UP-TO-DATE
> Task :app:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :app:dataBindingMergeDependencyArtifactsDebug UP-TO-DATE
> Task :app:generateDebugResources UP-TO-DATE
> Task :core:database:preBuild UP-TO-DATE
> Task :core:database:preDebugBuild UP-TO-DATE
> Task :core:database:generateDebugResources UP-TO-DATE
> Task :core:database:packageDebugResources UP-TO-DATE
> Task :ndi:sdk-bridge:syncNdiRuntimeLibs
> Task :ndi:sdk-bridge:preBuild
> Task :ndi:sdk-bridge:preDebugBuild
> Task :ndi:sdk-bridge:generateDebugResources UP-TO-DATE
> Task :ndi:sdk-bridge:packageDebugResources UP-TO-DATE
> Task :feature:ndi-browser:data:preBuild UP-TO-DATE
> Task :feature:ndi-browser:data:preDebugBuild UP-TO-DATE
> Task :feature:ndi-browser:data:generateDebugResources UP-TO-DATE
> Task :feature:ndi-browser:data:packageDebugResources UP-TO-DATE
> Task :feature:ndi-browser:domain:preBuild UP-TO-DATE
> Task :feature:ndi-browser:domain:preDebugBuild UP-TO-DATE
> Task :feature:ndi-browser:domain:generateDebugResources UP-TO-DATE
> Task :feature:ndi-browser:domain:packageDebugResources UP-TO-DATE
> Task :feature:ndi-browser:presentation:preBuild UP-TO-DATE
> Task :feature:ndi-browser:presentation:preDebugBuild UP-TO-DATE
> Task :feature:ndi-browser:presentation:generateDebugResources UP-TO-DATE
> Task :feature:ndi-browser:presentation:packageDebugResources UP-TO-DATE
> Task :feature:theme-editor:data:preBuild UP-TO-DATE
> Task :feature:theme-editor:data:preDebugBuild UP-TO-DATE
> Task :feature:theme-editor:data:generateDebugResources UP-TO-DATE
> Task :feature:theme-editor:data:packageDebugResources UP-TO-DATE
> Task :feature:theme-editor:domain:preBuild UP-TO-DATE
> Task :feature:theme-editor:domain:preDebugBuild UP-TO-DATE
> Task :feature:theme-editor:domain:generateDebugResources UP-TO-DATE
> Task :feature:theme-editor:domain:packageDebugResources UP-TO-DATE
> Task :feature:theme-editor:presentation:preBuild UP-TO-DATE
> Task :feature:theme-editor:presentation:preDebugBuild UP-TO-DATE
> Task :feature:theme-editor:presentation:generateDebugResources UP-TO-DATE
> Task :feature:theme-editor:presentation:packageDebugResources UP-TO-DATE
> Task :app:packageDebugResources UP-TO-DATE
> Task :core:database:processDebugNavigationResources UP-TO-DATE
> Task :ndi:sdk-bridge:processDebugNavigationResources UP-TO-DATE
> Task :feature:ndi-browser:data:processDebugNavigationResources UP-TO-DATE
> Task :feature:ndi-browser:domain:processDebugNavigationResources UP-TO-DATE
> Task :feature:ndi-browser:presentation:processDebugNavigationResources UP-TO-DATE
> Task :feature:theme-editor:data:processDebugNavigationResources UP-TO-DATE
> Task :feature:theme-editor:domain:processDebugNavigationResources UP-TO-DATE
> Task :feature:theme-editor:presentation:processDebugNavigationResources UP-TO-DATE
> Task :app:processDebugNavigationResources UP-TO-DATE
> Task :app:parseDebugLocalResources UP-TO-DATE
> Task :core:database:parseDebugLocalResources UP-TO-DATE
> Task :core:database:generateDebugRFile UP-TO-DATE
> Task :ndi:sdk-bridge:parseDebugLocalResources UP-TO-DATE
> Task :ndi:sdk-bridge:generateDebugRFile UP-TO-DATE
> Task :feature:ndi-browser:data:parseDebugLocalResources UP-TO-DATE
> Task :feature:ndi-browser:data:generateDebugRFile UP-TO-DATE
> Task :feature:ndi-browser:domain:parseDebugLocalResources UP-TO-DATE
> Task :feature:ndi-browser:domain:generateDebugRFile UP-TO-DATE
> Task :feature:ndi-browser:presentation:dataBindingMergeDependencyArtifactsDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:parseDebugLocalResources UP-TO-DATE
> Task :feature:ndi-browser:presentation:dataBindingGenBaseClassesDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:generateDebugRFile UP-TO-DATE
> Task :feature:theme-editor:data:parseDebugLocalResources UP-TO-DATE
> Task :feature:theme-editor:data:generateDebugRFile UP-TO-DATE
> Task :feature:theme-editor:domain:parseDebugLocalResources UP-TO-DATE
> Task :feature:theme-editor:domain:generateDebugRFile UP-TO-DATE
> Task :feature:theme-editor:presentation:dataBindingMergeDependencyArtifactsDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:parseDebugLocalResources UP-TO-DATE
> Task :feature:theme-editor:presentation:dataBindingGenBaseClassesDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:generateDebugRFile UP-TO-DATE
> Task :app:generateDebugBuildConfig
> Task :app:generateDebugRFile UP-TO-DATE
> Task :core:database:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :app:mergeDebugResources
> Task :core:database:kspDebugKotlin UP-TO-DATE
> Task :app:dataBindingGenBaseClassesDebug
> Task :core:database:compileDebugKotlin UP-TO-DATE
> Task :core:database:javaPreCompileDebug UP-TO-DATE
> Task :core:database:compileDebugJavaWithJavac NO-SOURCE
> Task :core:database:bundleLibCompileToJarDebug UP-TO-DATE
> Task :ndi:sdk-bridge:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :ndi:sdk-bridge:compileDebugKotlin UP-TO-DATE
> Task :ndi:sdk-bridge:javaPreCompileDebug UP-TO-DATE
> Task :ndi:sdk-bridge:compileDebugJavaWithJavac NO-SOURCE
> Task :ndi:sdk-bridge:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:ndi-browser:data:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:ndi-browser:domain:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:ndi-browser:domain:compileDebugKotlin UP-TO-DATE
> Task :feature:ndi-browser:domain:javaPreCompileDebug UP-TO-DATE
> Task :feature:ndi-browser:domain:compileDebugJavaWithJavac NO-SOURCE
> Task :feature:ndi-browser:domain:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:ndi-browser:data:compileDebugKotlin UP-TO-DATE
> Task :feature:ndi-browser:data:javaPreCompileDebug UP-TO-DATE
> Task :feature:ndi-browser:data:compileDebugJavaWithJavac NO-SOURCE
> Task :feature:ndi-browser:data:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:ndi-browser:presentation:compileDebugKotlin UP-TO-DATE
> Task :feature:ndi-browser:presentation:javaPreCompileDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:compileDebugJavaWithJavac UP-TO-DATE
> Task :feature:ndi-browser:presentation:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:theme-editor:data:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:theme-editor:domain:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:theme-editor:domain:compileDebugKotlin UP-TO-DATE
> Task :feature:theme-editor:domain:javaPreCompileDebug UP-TO-DATE
> Task :feature:theme-editor:domain:compileDebugJavaWithJavac NO-SOURCE
> Task :feature:theme-editor:domain:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:theme-editor:data:compileDebugKotlin UP-TO-DATE
> Task :feature:theme-editor:data:javaPreCompileDebug UP-TO-DATE
> Task :feature:theme-editor:data:compileDebugJavaWithJavac NO-SOURCE
> Task :feature:theme-editor:data:bundleLibCompileToJarDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:checkKotlinGradlePluginConfigurationErrors SKIPPED
> Task :feature:theme-editor:presentation:compileDebugKotlin UP-TO-DATE
> Task :feature:theme-editor:presentation:javaPreCompileDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:compileDebugJavaWithJavac UP-TO-DATE
> Task :feature:theme-editor:presentation:bundleLibCompileToJarDebug UP-TO-DATE
> Task :app:javaPreCompileDebug UP-TO-DATE
> Task :app:generateDebugAssets UP-TO-DATE
> Task :core:database:generateDebugAssets UP-TO-DATE
> Task :core:database:mergeDebugAssets UP-TO-DATE
> Task :ndi:sdk-bridge:generateDebugAssets UP-TO-DATE
> Task :ndi:sdk-bridge:mergeDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:data:generateDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:data:mergeDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:domain:generateDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:domain:mergeDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:presentation:generateDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:presentation:mergeDebugAssets UP-TO-DATE
> Task :feature:theme-editor:data:generateDebugAssets UP-TO-DATE
> Task :feature:theme-editor:data:mergeDebugAssets UP-TO-DATE
> Task :feature:theme-editor:domain:generateDebugAssets UP-TO-DATE
> Task :feature:theme-editor:domain:mergeDebugAssets UP-TO-DATE
> Task :feature:theme-editor:presentation:generateDebugAssets UP-TO-DATE
> Task :feature:theme-editor:presentation:mergeDebugAssets UP-TO-DATE
> Task :app:mergeDebugAssets UP-TO-DATE
> Task :app:compressDebugAssets UP-TO-DATE
> Task :feature:ndi-browser:data:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :feature:theme-editor:data:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :core:database:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :feature:ndi-browser:domain:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :feature:theme-editor:domain:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :ndi:sdk-bridge:bundleLibRuntimeToJarDebug UP-TO-DATE
> Task :app:desugarDebugFileDependencies UP-TO-DATE
> Task :core:database:writeDebugAarMetadata UP-TO-DATE
> Task :ndi:sdk-bridge:writeDebugAarMetadata UP-TO-DATE
> Task :feature:ndi-browser:data:writeDebugAarMetadata UP-TO-DATE
> Task :feature:ndi-browser:domain:writeDebugAarMetadata UP-TO-DATE
> Task :feature:ndi-browser:presentation:writeDebugAarMetadata UP-TO-DATE
> Task :feature:theme-editor:data:writeDebugAarMetadata UP-TO-DATE
> Task :feature:theme-editor:domain:writeDebugAarMetadata UP-TO-DATE
> Task :feature:theme-editor:presentation:writeDebugAarMetadata UP-TO-DATE
> Task :app:checkDebugAarMetadata UP-TO-DATE
> Task :app:compileDebugNavigationResources UP-TO-DATE
> Task :app:mapDebugSourceSetPaths
> Task :app:createDebugCompatibleScreenManifests
> Task :app:extractDeepLinksDebug UP-TO-DATE
> Task :core:database:extractDeepLinksDebug UP-TO-DATE
> Task :core:database:processDebugManifest UP-TO-DATE
> Task :ndi:sdk-bridge:extractDeepLinksDebug UP-TO-DATE
> Task :ndi:sdk-bridge:processDebugManifest UP-TO-DATE
> Task :feature:ndi-browser:data:extractDeepLinksDebug UP-TO-DATE
> Task :feature:ndi-browser:data:processDebugManifest UP-TO-DATE
> Task :feature:ndi-browser:domain:extractDeepLinksDebug UP-TO-DATE
> Task :feature:ndi-browser:domain:processDebugManifest UP-TO-DATE
> Task :feature:ndi-browser:presentation:extractDeepLinksDebug UP-TO-DATE
> Task :feature:ndi-browser:presentation:processDebugManifest UP-TO-DATE
> Task :feature:theme-editor:data:extractDeepLinksDebug UP-TO-DATE
> Task :feature:theme-editor:data:processDebugManifest UP-TO-DATE
> Task :feature:theme-editor:domain:extractDeepLinksDebug UP-TO-DATE
> Task :feature:theme-editor:domain:processDebugManifest UP-TO-DATE
> Task :feature:theme-editor:presentation:extractDeepLinksDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:processDebugManifest UP-TO-DATE
> Task :app:processDebugMainManifest
> Task :app:processDebugManifest
> Task :core:database:compileDebugLibraryResources UP-TO-DATE
> Task :ndi:sdk-bridge:compileDebugLibraryResources UP-TO-DATE
> Task :feature:ndi-browser:data:compileDebugLibraryResources UP-TO-DATE
> Task :feature:ndi-browser:domain:compileDebugLibraryResources UP-TO-DATE
> Task :app:processDebugManifestForPackage
> Task :feature:theme-editor:data:compileDebugLibraryResources UP-TO-DATE
> Task :feature:theme-editor:domain:compileDebugLibraryResources UP-TO-DATE
> Task :feature:theme-editor:presentation:compileDebugLibraryResources UP-TO-DATE
> Task :core:database:processDebugJavaRes UP-TO-DATE
> Task :ndi:sdk-bridge:processDebugJavaRes UP-TO-DATE
> Task :feature:ndi-browser:data:processDebugJavaRes UP-TO-DATE
> Task :feature:ndi-browser:domain:processDebugJavaRes UP-TO-DATE
> Task :feature:ndi-browser:presentation:compileDebugLibraryResources
> Task :feature:ndi-browser:presentation:processDebugJavaRes UP-TO-DATE
> Task :feature:theme-editor:data:processDebugJavaRes UP-TO-DATE
> Task :feature:theme-editor:domain:processDebugJavaRes UP-TO-DATE
> Task :feature:theme-editor:presentation:processDebugJavaRes UP-TO-DATE
> Task :app:checkDebugDuplicateClasses UP-TO-DATE
> Task :app:mergeExtDexDebug UP-TO-DATE
> Task :feature:theme-editor:data:bundleLibRuntimeToDirDebug UP-TO-DATE
> Task :core:database:bundleLibRuntimeToDirDebug UP-TO-DATE
> Task :feature:theme-editor:presentation:bundleLibRuntimeToDirDebug UP-TO-DATE
> Task :feature:theme-editor:domain:bundleLibRuntimeToDirDebug UP-TO-DATE
> Task :app:mergeDebugJniLibFolders UP-TO-DATE
> Task :core:database:mergeDebugJniLibFolders UP-TO-DATE
> Task :core:database:mergeDebugNativeLibs NO-SOURCE
> Task :core:database:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :ndi:sdk-bridge:bundleLibRuntimeToDirDebug
> Task :feature:ndi-browser:domain:bundleLibRuntimeToDirDebug
> Task :feature:ndi-browser:presentation:bundleLibRuntimeToDirDebug
> Task :feature:ndi-browser:data:bundleLibRuntimeToDirDebug
> Task :ndi:sdk-bridge:configureCMakeDebug[arm64-v8a]
> Task :ndi:sdk-bridge:buildCMakeDebug[arm64-v8a]
> Task :ndi:sdk-bridge:configureCMakeDebug[armeabi-v7a]
> Task :ndi:sdk-bridge:buildCMakeDebug[armeabi-v7a]
> Task :ndi:sdk-bridge:mergeDebugJniLibFolders UP-TO-DATE
> Task :ndi:sdk-bridge:mergeDebugNativeLibs UP-TO-DATE
> Task :feature:ndi-browser:data:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:ndi-browser:data:mergeDebugNativeLibs NO-SOURCE
> Task :feature:ndi-browser:data:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :feature:ndi-browser:domain:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:ndi-browser:domain:mergeDebugNativeLibs NO-SOURCE
> Task :feature:ndi-browser:domain:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :feature:ndi-browser:presentation:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:ndi-browser:presentation:mergeDebugNativeLibs NO-SOURCE
> Task :feature:ndi-browser:presentation:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :feature:theme-editor:data:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:theme-editor:data:mergeDebugNativeLibs NO-SOURCE
> Task :feature:theme-editor:data:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :feature:theme-editor:domain:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:theme-editor:domain:mergeDebugNativeLibs NO-SOURCE
> Task :feature:theme-editor:domain:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :feature:theme-editor:presentation:mergeDebugJniLibFolders UP-TO-DATE
> Task :feature:theme-editor:presentation:mergeDebugNativeLibs NO-SOURCE
> Task :feature:theme-editor:presentation:copyDebugJniLibsProjectOnly UP-TO-DATE
> Task :app:validateSigningDebug UP-TO-DATE
> Task :app:writeDebugAppMetadata UP-TO-DATE
> Task :app:writeDebugSigningConfigVersions UP-TO-DATE
> Task :ndi:sdk-bridge:copyDebugJniLibsProjectOnly
> Task :app:mergeDebugNativeLibs
> Task :app:stripDebugDebugSymbols
> Task :app:processDebugResources
> Task :app:mergeLibDexDebug
> Task :app:compileDebugKotlin
> Task :app:compileDebugJavaWithJavac
> Task :app:dexBuilderDebug
> Task :app:mergeDebugGlobalSynthetics UP-TO-DATE
> Task :app:processDebugJavaRes UP-TO-DATE
> Task :app:mergeDebugJavaResource UP-TO-DATE
> Task :app:mergeProjectDexDebug
> Task :app:packageDebug
> Task :app:createDebugApkListingFileRedirect
> Task :app:assembleDebug

> Task :app:renameDebugApk
  Γ£ô APK exported to C:\githubrepos\NDI-for-Android\exports\ndi-for-android-0.7.13.apk

BUILD SUCCESSFUL in 53s
200 actionable tasks: 30 executed, 170 up-to-date
