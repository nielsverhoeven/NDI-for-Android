# Keep native method signatures used by the NDI bridge.
-keepclasseswithmembernames class * {
    native <methods>;
}

# Preserve Room schema metadata for generated implementations.
-keep class androidx.room.** { *; }
