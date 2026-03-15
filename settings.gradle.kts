pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "NDI-for-Android"

include(
    ":app",
    ":core:model",
    ":core:database",
    ":core:testing",
    ":feature:ndi-browser:domain",
    ":feature:ndi-browser:data",
    ":feature:ndi-browser:presentation",
    ":ndi:sdk-bridge",
)
