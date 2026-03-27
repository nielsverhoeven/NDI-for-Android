#include <jni.h>

#include <algorithm>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

#ifdef NDI_SDK_AVAILABLE
#include "Processing.NDI.Lib.h"
#endif

namespace {

std::mutex g_state_mutex;
std::atomic<bool> g_receiver_running{false};
std::thread g_receiver_thread;

struct DiscoveredSource {
    std::string source_id;
    std::string display_name;
    std::string url_address;
};

std::vector<DiscoveredSource> g_last_discovered_sources;
std::unordered_map<std::string, std::string> g_discovered_url_by_source_id;
std::string g_discovery_extra_ips;

std::vector<jint> g_latest_argb_frame;
int g_latest_frame_width = 0;
int g_latest_frame_height = 0;

#ifdef NDI_SDK_AVAILABLE
NDIlib_recv_instance_t g_recv_instance = nullptr;
NDIlib_send_instance_t g_send_instance = nullptr;
#endif

void clear_latest_frame_locked() {
    g_latest_argb_frame.clear();
    g_latest_frame_width = 0;
    g_latest_frame_height = 0;
}

void clear_latest_frame() {
    std::lock_guard<std::mutex> lock(g_state_mutex);
    clear_latest_frame_locked();
}

void throw_java_runtime_exception(JNIEnv* env, const char* message) {
    jclass runtime_exception_class = env->FindClass("java/lang/RuntimeException");
    if (runtime_exception_class != nullptr) {
        env->ThrowNew(runtime_exception_class, message);
    }
}

jobjectArray to_java_string_array(JNIEnv* env, const std::vector<std::string>& values) {
    jclass string_class = env->FindClass("java/lang/String");
    jobjectArray result = env->NewObjectArray(static_cast<jsize>(values.size()), string_class, nullptr);
    for (jsize index = 0; index < static_cast<jsize>(values.size()); ++index) {
        jstring value = env->NewStringUTF(values[index].c_str());
        env->SetObjectArrayElement(result, index, value);
        env->DeleteLocalRef(value);
    }
    return result;
}

#ifdef NDI_SDK_AVAILABLE
bool ensure_ndi_initialized() {
    static std::once_flag initialization_flag;
    static bool initialized = false;
    std::call_once(initialization_flag, []() {
        initialized = NDIlib_initialize();
    });
    return initialized;
}

std::vector<DiscoveredSource> discover_sources_native() {
    if (!ensure_ndi_initialized()) {
        return {};
    }

    NDIlib_find_create_t create_description;
    std::memset(&create_description, 0, sizeof(create_description));
    create_description.show_local_sources = true;
    create_description.p_groups = nullptr;
    create_description.p_extra_ips = g_discovery_extra_ips.empty() ? nullptr : g_discovery_extra_ips.c_str();

    NDIlib_find_instance_t find_instance = NDIlib_find_create_v2(&create_description);
    if (find_instance == nullptr) {
        return {};
    }

    std::vector<DiscoveredSource> discovered;
    NDIlib_find_wait_for_sources(find_instance, 3000);

    uint32_t source_count = 0;
    const NDIlib_source_t* sources = NDIlib_find_get_current_sources(find_instance, &source_count);
    if (sources != nullptr) {
        discovered.reserve(source_count);
        for (uint32_t index = 0; index < source_count; ++index) {
            const char* ndi_name = sources[index].p_ndi_name;
            const char* url_address = sources[index].p_url_address;
            const std::string source_id = ndi_name != nullptr ? ndi_name : (url_address != nullptr ? url_address : "");
            const std::string display_name = ndi_name != nullptr ? ndi_name : source_id;
            const std::string url = url_address != nullptr ? url_address : "";

            if (!source_id.empty()) {
                discovered.push_back(DiscoveredSource {
                    .source_id = source_id,
                    .display_name = display_name,
                    .url_address = url,
                });
            }
        }
    }

    NDIlib_find_destroy(find_instance);
    return discovered;
}

void stop_sender_native_locked() {
    if (g_send_instance != nullptr) {
        NDIlib_send_destroy(g_send_instance);
        g_send_instance = nullptr;
    }
}

void start_local_screen_share_sender_native(const std::string& stream_name) {
    if (!ensure_ndi_initialized()) {
        throw std::runtime_error("NDI SDK failed to initialize");
    }

    std::lock_guard<std::mutex> lock(g_state_mutex);
    stop_sender_native_locked();

    NDIlib_send_create_t create_description;
    std::memset(&create_description, 0, sizeof(create_description));
    create_description.p_ndi_name = stream_name.c_str();
    create_description.p_groups = nullptr;
    create_description.clock_video = true;
    create_description.clock_audio = false;

    g_send_instance = NDIlib_send_create(&create_description);
    if (g_send_instance == nullptr) {
        throw std::runtime_error("Unable to create NDI sender");
    }
}

void submit_local_screen_share_frame_native(JNIEnv* env, jint width, jint height, jintArray argb_pixels) {
    if (width <= 0 || height <= 0 || argb_pixels == nullptr) {
        throw std::runtime_error("Screen-share frame is invalid");
    }

    const jsize pixel_count = static_cast<jsize>(width * height);
    if (env->GetArrayLength(argb_pixels) < pixel_count) {
        throw std::runtime_error("Screen-share frame buffer is too small");
    }

    std::vector<jint> input(static_cast<size_t>(pixel_count));
    env->GetIntArrayRegion(argb_pixels, 0, pixel_count, input.data());

    std::vector<uint8_t> bgra(static_cast<size_t>(pixel_count) * 4);
    for (jsize index = 0; index < pixel_count; ++index) {
        const uint32_t pixel = static_cast<uint32_t>(input[static_cast<size_t>(index)]);
        const size_t offset = static_cast<size_t>(index) * 4;
        bgra[offset + 0] = static_cast<uint8_t>(pixel & 0xFF);
        bgra[offset + 1] = static_cast<uint8_t>((pixel >> 8) & 0xFF);
        bgra[offset + 2] = static_cast<uint8_t>((pixel >> 16) & 0xFF);
        bgra[offset + 3] = static_cast<uint8_t>((pixel >> 24) & 0xFF);
    }

    std::lock_guard<std::mutex> lock(g_state_mutex);
    if (g_send_instance == nullptr) {
        throw std::runtime_error("NDI sender is not active");
    }

    NDIlib_video_frame_v2_t video_frame;
    std::memset(&video_frame, 0, sizeof(video_frame));
    video_frame.xres = width;
    video_frame.yres = height;
    video_frame.FourCC = NDIlib_FourCC_video_type_BGRA;
    video_frame.frame_rate_N = 30;
    video_frame.frame_rate_D = 1;
    video_frame.picture_aspect_ratio = static_cast<float>(width) / static_cast<float>(height);
    video_frame.frame_format_type = NDIlib_frame_format_type_progressive;
    video_frame.timecode = NDIlib_send_timecode_synthesize;
    video_frame.p_data = bgra.data();
    video_frame.line_stride_in_bytes = width * 4;

    NDIlib_send_send_video_v2(g_send_instance, &video_frame);
}

void stop_receiver_native_locked() {
    g_receiver_running = false;
    if (g_receiver_thread.joinable()) {
        g_receiver_thread.join();
    }
    if (g_recv_instance != nullptr) {
        NDIlib_recv_destroy(g_recv_instance);
        g_recv_instance = nullptr;
    }
    clear_latest_frame_locked();
}

void start_receiver_native(const std::string& source_id) {
    std::lock_guard<std::mutex> lock(g_state_mutex);

    stop_receiver_native_locked();

    if (!ensure_ndi_initialized()) {
        return;
    }

    NDIlib_source_t source_to_connect;
    std::memset(&source_to_connect, 0, sizeof(source_to_connect));

    const bool looks_like_ndi_name = source_id.find(" (") != std::string::npos && !source_id.empty() && source_id.back() == ')';
    std::string discovered_url;
    auto discovered_it = g_discovered_url_by_source_id.find(source_id);
    if (discovered_it != g_discovered_url_by_source_id.end()) {
        discovered_url = discovered_it->second;
    }

    if (looks_like_ndi_name) {
        source_to_connect.p_ndi_name = source_id.c_str();
        source_to_connect.p_url_address = discovered_url.empty() ? nullptr : discovered_url.c_str();
    } else {
        source_to_connect.p_ndi_name = nullptr;
        source_to_connect.p_url_address = source_id.c_str();
    }

    NDIlib_recv_create_v3_t recv_create;
    std::memset(&recv_create, 0, sizeof(recv_create));
    recv_create.source_to_connect_to = source_to_connect;
    recv_create.color_format = NDIlib_recv_color_format_BGRX_BGRA;
    recv_create.bandwidth = NDIlib_recv_bandwidth_highest;
    recv_create.allow_video_fields = false;

    g_recv_instance = NDIlib_recv_create_v3(&recv_create);
    if (g_recv_instance == nullptr) {
        return;
    }

    NDIlib_recv_connect(g_recv_instance, &source_to_connect);

    g_receiver_running = true;
    g_receiver_thread = std::thread([]() {
        while (g_receiver_running.load()) {
            NDIlib_video_frame_v2_t video_frame;
            std::memset(&video_frame, 0, sizeof(video_frame));

            const auto frame_type = NDIlib_recv_capture_v3(
                g_recv_instance,
                &video_frame,
                nullptr,
                nullptr,
                1000
            );

            if (frame_type == NDIlib_frame_type_video && video_frame.p_data != nullptr && video_frame.xres > 0 && video_frame.yres > 0) {
                const int width = video_frame.xres;
                const int height = video_frame.yres;
                const int stride = video_frame.line_stride_in_bytes > 0 ? video_frame.line_stride_in_bytes : width * 4;

                std::vector<jint> argb_pixels;
                argb_pixels.resize(static_cast<size_t>(width) * static_cast<size_t>(height));

                const bool is_bgr_order =
                    video_frame.FourCC == NDIlib_FourCC_type_BGRA ||
                    video_frame.FourCC == NDIlib_FourCC_video_type_BGRA ||
                    video_frame.FourCC == NDIlib_FourCC_type_BGRX ||
                    video_frame.FourCC == NDIlib_FourCC_video_type_BGRX;

                for (int y = 0; y < height; ++y) {
                    const uint8_t* row = video_frame.p_data + static_cast<size_t>(y) * static_cast<size_t>(stride);
                    for (int x = 0; x < width; ++x) {
                        const uint8_t c0 = row[x * 4 + 0];
                        const uint8_t c1 = row[x * 4 + 1];
                        const uint8_t c2 = row[x * 4 + 2];
                        const uint8_t c3 = row[x * 4 + 3];

                        const uint8_t r = is_bgr_order ? c2 : c0;
                        const uint8_t g = c1;
                        const uint8_t b = is_bgr_order ? c0 : c2;
                        const uint8_t a = 0xFF;

                        argb_pixels[static_cast<size_t>(y) * static_cast<size_t>(width) + static_cast<size_t>(x)] =
                            (static_cast<jint>(a) << 24) |
                            (static_cast<jint>(r) << 16) |
                            (static_cast<jint>(g) << 8) |
                            static_cast<jint>(b);
                    }
                }

                {
                    std::lock_guard<std::mutex> lock(g_state_mutex);
                    g_latest_frame_width = width;
                    g_latest_frame_height = height;
                    g_latest_argb_frame.swap(argb_pixels);
                }

                NDIlib_recv_free_video_v2(g_recv_instance, &video_frame);
            } else if (frame_type == NDIlib_frame_type_none) {
                continue;
            }
        }
    });
}
#endif

}  // namespace

extern "C" JNIEXPORT jobjectArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeDiscoverSourceIds(
    JNIEnv* env,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    auto discovered = discover_sources_native();
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        g_last_discovered_sources = discovered;
        g_discovered_url_by_source_id.clear();
        for (const auto& entry : discovered) {
            if (!entry.url_address.empty()) {
                g_discovered_url_by_source_id[entry.source_id] = entry.url_address;
                if (entry.display_name != entry.source_id) {
                    g_discovered_url_by_source_id[entry.display_name] = entry.url_address;
                }
            }
        }
    }
    std::vector<std::string> source_ids;
    source_ids.reserve(discovered.size());
    for (const auto& entry : discovered) {
        source_ids.push_back(entry.source_id);
    }
    return to_java_string_array(env, source_ids);
#else
    return env->NewObjectArray(0, env->FindClass("java/lang/String"), nullptr);
#endif
}

extern "C" JNIEXPORT jobjectArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeDiscoverDisplayNames(
    JNIEnv* env,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::vector<std::string> display_names;
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        if (g_last_discovered_sources.empty()) {
            g_last_discovered_sources = discover_sources_native();
        }
        display_names.reserve(g_last_discovered_sources.size());
        for (const auto& entry : g_last_discovered_sources) {
            display_names.push_back(entry.display_name);
        }
    }
    return to_java_string_array(env, display_names);
#else
    return env->NewObjectArray(0, env->FindClass("java/lang/String"), nullptr);
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSetDiscoveryExtraIps(
    JNIEnv* env,
    jobject /* this */,
    jstring extra_ips_csv
) {
#ifdef NDI_SDK_AVAILABLE
    const char* raw_value = extra_ips_csv != nullptr ? env->GetStringUTFChars(extra_ips_csv, nullptr) : nullptr;
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        g_discovery_extra_ips = raw_value != nullptr ? raw_value : "";
        g_last_discovered_sources.clear();
        g_discovered_url_by_source_id.clear();
    }
    if (raw_value != nullptr) {
        env->ReleaseStringUTFChars(extra_ips_csv, raw_value);
    }
#else
    (void)env;
    (void)extra_ips_csv;
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartReceiver(
    JNIEnv* env,
    jobject /* this */,
    jstring source_id
) {
#ifdef NDI_SDK_AVAILABLE
    if (source_id == nullptr) {
        throw_java_runtime_exception(env, "Source id is required");
        return;
    }

    const char* raw_source_id = env->GetStringUTFChars(source_id, nullptr);
    if (raw_source_id == nullptr || std::strlen(raw_source_id) == 0) {
        if (raw_source_id != nullptr) {
            env->ReleaseStringUTFChars(source_id, raw_source_id);
        }
        throw_java_runtime_exception(env, "Source id is empty");
        return;
    }

    const std::string source(raw_source_id);
    env->ReleaseStringUTFChars(source_id, raw_source_id);

    start_receiver_native(source);
#else
    throw_java_runtime_exception(env, "NDI SDK is not linked in this build");
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopReceiver(
    JNIEnv* /* env */,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    stop_receiver_native_locked();
#else
    clear_latest_frame();
#endif
}

extern "C" JNIEXPORT jintArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetLatestReceiverFrameArgb(
    JNIEnv* env,
    jobject /* this */
) {
    std::lock_guard<std::mutex> lock(g_state_mutex);
    if (g_latest_argb_frame.empty()) {
        return nullptr;
    }

    jintArray result = env->NewIntArray(static_cast<jsize>(g_latest_argb_frame.size()));
    env->SetIntArrayRegion(result, 0, static_cast<jsize>(g_latest_argb_frame.size()), g_latest_argb_frame.data());
    return result;
}

extern "C" JNIEXPORT jint JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetLatestReceiverFrameWidth(
    JNIEnv* /* env */,
    jobject /* this */
) {
    std::lock_guard<std::mutex> lock(g_state_mutex);
    return g_latest_frame_width;
}

extern "C" JNIEXPORT jint JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetLatestReceiverFrameHeight(
    JNIEnv* /* env */,
    jobject /* this */
) {
    std::lock_guard<std::mutex> lock(g_state_mutex);
    return g_latest_frame_height;
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartSender(
    JNIEnv* env,
    jobject /* this */,
    jstring /* sourceId */,
    jstring /* streamName */
) {
    throw_java_runtime_exception(env, "Forwarding an existing NDI source is not implemented in this build");
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopSender(
    JNIEnv* /* env */,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    stop_sender_native_locked();
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStartLocalScreenShareSender(
    JNIEnv* env,
    jobject /* this */,
    jstring stream_name
) {
#ifdef NDI_SDK_AVAILABLE
    if (stream_name == nullptr) {
        throw_java_runtime_exception(env, "Stream name is required");
        return;
    }

    const char* raw_stream_name = env->GetStringUTFChars(stream_name, nullptr);
    if (raw_stream_name == nullptr || std::strlen(raw_stream_name) == 0) {
        if (raw_stream_name != nullptr) {
            env->ReleaseStringUTFChars(stream_name, raw_stream_name);
        }
        throw_java_runtime_exception(env, "Stream name is empty");
        return;
    }

    try {
        start_local_screen_share_sender_native(raw_stream_name);
    } catch (const std::exception& error) {
        throw_java_runtime_exception(env, error.what());
    }

    env->ReleaseStringUTFChars(stream_name, raw_stream_name);
#else
    throw_java_runtime_exception(env, "NDI SDK is not linked in this build");
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSubmitLocalScreenShareFrameArgb(
    JNIEnv* env,
    jobject /* this */,
    jint width,
    jint height,
    jintArray argb_pixels
) {
#ifdef NDI_SDK_AVAILABLE
    try {
        submit_local_screen_share_frame_native(env, width, height, argb_pixels);
    } catch (const std::exception& error) {
        throw_java_runtime_exception(env, error.what());
    }
#else
    throw_java_runtime_exception(env, "NDI SDK is not linked in this build");
#endif
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeStopLocalScreenShareSender(
    JNIEnv* /* env */,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    stop_sender_native_locked();
#endif
}

