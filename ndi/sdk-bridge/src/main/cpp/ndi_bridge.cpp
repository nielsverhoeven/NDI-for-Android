#include <jni.h>

extern "C" JNIEXPORT jobjectArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeDiscoverSourceIds(
    JNIEnv* env,
    jobject /* this */
) {
    return env->NewObjectArray(0, env->FindClass("java/lang/String"), nullptr);
}

extern "C" JNIEXPORT jobjectArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeDiscoverDisplayNames(
    JNIEnv* env,
    jobject /* this */
) {
    return env->NewObjectArray(0, env->FindClass("java/lang/String"), nullptr);
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartReceiver(
    JNIEnv* /* env */,
    jobject /* this */,
    jstring /* sourceId */
) {
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopReceiver(
    JNIEnv* /* env */,
    jobject /* this */
) {
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartSender(
    JNIEnv* /* env */,
    jobject /* this */,
    jstring /* sourceId */,
    jstring /* streamName */
) {
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopSender(
    JNIEnv* /* env */,
    jobject /* this */
) {
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartLocalScreenShareSender(
    JNIEnv* /* env */,
    jobject /* this */,
    jstring /* streamName */
) {
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopLocalScreenShareSender(
    JNIEnv* /* env */,
    jobject /* this */
) {
}

