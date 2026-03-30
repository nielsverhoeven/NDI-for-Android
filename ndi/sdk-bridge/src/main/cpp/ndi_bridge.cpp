#include <jni.h>
#include <android/log.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <deque>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

// POSIX for setenv/unsetenv
#include <stdlib.h>

#ifdef NDI_SDK_AVAILABLE
#include "Processing.NDI.Lib.h"
#include "Processing.NDI.SendListener.h"
#endif

namespace {

constexpr const char* kNativeLogTag = "NdiNative";

void log_native_info(const std::string& message) {
    __android_log_print(ANDROID_LOG_INFO, kNativeLogTag, "%s", message.c_str());
}

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

struct ReceiverQualityPolicy {
    int bandwidth_tier = 0;
    int target_fps = 30;
    int target_width = 0;
    int target_height = 0;
    std::string codec_pref = "adaptive";
};

ReceiverQualityPolicy g_quality_policy;
uint64_t g_prev_total_video = 0;
uint64_t g_prev_dropped_video = 0;
float g_drop_percent_window = 0.0f;
float g_measured_receiver_fps = 0.0f;
std::deque<std::chrono::steady_clock::time_point> g_video_frame_times;

#ifdef NDI_SDK_AVAILABLE
NDIlib_recv_instance_t g_recv_instance = nullptr;
NDIlib_send_instance_t g_send_instance = nullptr;
#endif

void clear_latest_frame_locked() {
    g_latest_argb_frame.clear();
    g_latest_frame_width = 0;
    g_latest_frame_height = 0;
    g_drop_percent_window = 0.0f;
    g_measured_receiver_fps = 0.0f;
    g_prev_total_video = 0;
    g_prev_dropped_video = 0;
    g_video_frame_times.clear();
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

std::string trim_copy(const std::string& input) {
    const size_t start = input.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) {
        return "";
    }
    const size_t end = input.find_last_not_of(" \t\r\n");
    return input.substr(start, end - start + 1);
}

// Use a plain mutex+atomic so we can reinitialize NDI when the discovery
// server endpoint changes (NDIlib reads NDI_DISCOVERY_SERVER only at init time).
static std::mutex g_ndi_init_mutex;
static std::atomic<bool> g_ndi_initialized{false};

std::string discovery_endpoints_csv_snapshot() {
    std::lock_guard<std::mutex> lock(g_state_mutex);
    return g_discovery_extra_ips;
}

// Set NDI_DISCOVERY_SERVER to the full comma-separated endpoint list.
// The NDI SDK (v5+) natively supports multiple discovery servers when
// given a comma-delimited value — all are contacted simultaneously.
void apply_ndi_discovery_env() {
    // On Android we configure discovery via HOME/.ndi/ndi-config.v1.json from Kotlin.
    // Do not force NDI_DISCOVERY_SERVER here: SDK env parsing differences can override
    // a valid config file and lead to empty source lists.
    unsetenv("NDI_DISCOVERY_SERVER");
    log_native_info("NDI_DISCOVERY_SERVER cleared (using ndi-config.v1.json)");
}

// Persistent finder — kept alive between discovery ticks so the SDK can
// accumulate its source list without a cold-start wait on every poll.
// Destroyed when the discovery endpoint list changes (before NDI reinit).
static std::mutex g_finder_mutex;
static std::mutex g_discovery_call_mutex;
static NDIlib_find_instance_t g_finder = nullptr;
static std::mutex g_send_listener_mutex;
static NDIlib_send_listener_instance_t g_send_listener = nullptr;
static std::string g_send_listener_server_url;

void destroy_send_listener_locked() {
    if (g_send_listener != nullptr) {
        NDIlib_send_listener_destroy(g_send_listener);
        g_send_listener = nullptr;
        g_send_listener_server_url.clear();
        log_native_info("send_listener destroyed");
    }
}

bool ensure_ndi_initialized() {
    if (g_ndi_initialized.load(std::memory_order_acquire)) {
        return true;
    }
    std::lock_guard<std::mutex> lock(g_ndi_init_mutex);
    if (g_ndi_initialized.load(std::memory_order_relaxed)) {
        return true;
    }
    // Must set NDI_DISCOVERY_SERVER BEFORE NDIlib_initialize; the SDK reads the
    // variable once during library initialization and caches the result.
    apply_ndi_discovery_env();
    const bool result = NDIlib_initialize();
    g_ndi_initialized.store(result, std::memory_order_release);
    if (result) {
        log_native_info(std::string("NDI init ok, SDK=") + NDIlib_version());
    } else {
        log_native_info("NDI init FAILED");
    }
    return result;
}

void append_find_sources(NDIlib_find_instance_t find_instance, std::vector<DiscoveredSource>& discovered) {
    uint32_t source_count = 0;
    const NDIlib_source_t* sources = NDIlib_find_get_current_sources(find_instance, &source_count);
    if (sources == nullptr) {
        return;
    }

    discovered.reserve(discovered.size() + source_count);
    for (uint32_t index = 0; index < source_count; ++index) {
        const char* ndi_name = sources[index].p_ndi_name;
        const char* url_address = sources[index].p_url_address;
        const std::string url = url_address != nullptr ? url_address : "";
        const std::string display_name = ndi_name != nullptr
            ? ndi_name
            : (!url.empty() ? url : "");

        // Use URL address as the canonical identity when available because it is
        // unique per announced source. Some discovery-server topologies can return
        // duplicate/overlapping NDI names, which collapses distinct sources when
        // deduplicating by source id on the Kotlin side.
        const std::string source_id = !url.empty() ? url : display_name;

        if (!source_id.empty()) {
            discovered.push_back(DiscoveredSource {
                .source_id = source_id,
                .display_name = display_name,
                .url_address = url,
            });
            log_native_info(
                "discover source[" + std::to_string(index) + "] id=" + source_id +
                " name=" + display_name + " url=" + url
            );
        }
    }
}

std::string first_discovery_server_url_snapshot() {
    const std::string csv = discovery_endpoints_csv_snapshot();
    if (csv.empty()) {
        return "";
    }
    const size_t comma = csv.find(',');
    if (comma == std::string::npos) {
        return trim_copy(csv);
    }
    return trim_copy(csv.substr(0, comma));
}

void append_send_listener_sources(std::vector<DiscoveredSource>& discovered) {
    const std::string server_url = first_discovery_server_url_snapshot();
    std::lock_guard<std::mutex> lock(g_send_listener_mutex);

    if (g_send_listener != nullptr && g_send_listener_server_url != server_url) {
        destroy_send_listener_locked();
    }

    if (g_send_listener == nullptr) {
        NDIlib_send_listener_create_t listener_create;
        std::memset(&listener_create, 0, sizeof(listener_create));
        listener_create.p_url_address = server_url.empty() ? nullptr : server_url.c_str();

        g_send_listener = NDIlib_send_listener_create(&listener_create);
        if (g_send_listener == nullptr) {
            log_native_info("send_listener create failed");
            return;
        }
        g_send_listener_server_url = server_url;
        log_native_info("send_listener created for " + server_url);
    }

    NDIlib_send_listener_wait_for_senders(g_send_listener, 1200);

    uint32_t sender_count = 0;
    const NDIlib_sender_t* senders = NDIlib_send_listener_get_senders(g_send_listener, &sender_count);
    if (senders != nullptr && sender_count > 0) {
        discovered.reserve(discovered.size() + sender_count);
        for (uint32_t index = 0; index < sender_count; ++index) {
            const std::string uuid = senders[index].p_uuid != nullptr ? senders[index].p_uuid : "";
            const std::string name = senders[index].p_name != nullptr ? senders[index].p_name : "";
            const std::string address = senders[index].p_address != nullptr ? senders[index].p_address : "";
            const int port = senders[index].port;

            std::string url = address;
            if (!url.empty() && port > 0) {
                url += ":" + std::to_string(port);
            }

            const std::string source_id = !uuid.empty() ? uuid : (!url.empty() ? url : name);
            const std::string display_name = !name.empty() ? name : source_id;

            if (!source_id.empty()) {
                discovered.push_back(DiscoveredSource {
                    .source_id = source_id,
                    .display_name = display_name,
                    .url_address = url,
                });
                log_native_info(
                    "send_listener source[" + std::to_string(index) + "] id=" + source_id +
                    " name=" + display_name + " url=" + url
                );
            }
        }
    }
}

// Canonical NDI source discovery using a persistent finder.
//
// Architecture (validated against official NDI SDK docs):
//   - NDI_DISCOVERY_SERVER is set to the full comma-separated list of
//     host:port endpoints BEFORE NDIlib_initialize(). The SDK v5+ contacts
//     all listed servers simultaneously via a single finder instance.
//   - g_finder is kept alive between discovery ticks. Re-creating the finder
//     on every poll resets the SDK's accumulated source list and restarts the
//     warm-up wait from zero; a persistent finder avoids this.
//   - NDIlib_send_listener_* (the prior fallback) is the sender-monitoring API
//     for the new advertiser protocol (NDI 6.3+). It cannot see legacy NDI
//     sources that registered via NDIlib_send_create, and must NOT be used
//     as a discovery path.
//   - p_extra_ips in NDIlib_find_create_t probes specific host machines for
//     sources running ON them (unicast reach). Passing a Discovery Server IP
//     there does not query its registry; that path is also removed.
//
// Lock discipline:
//   g_finder_mutex — protects g_finder pointer.
//   Held briefly to read/create the pointer, released before the blocking wait,
//   reacquired after the wait to read sources. If the finder was destroyed while
//   we waited (endpoint change), we return empty and let the next tick retry.
std::vector<DiscoveredSource> discover_sources_native() {
    // NDI finder calls are not guaranteed to be thread-safe when issued in
    // parallel against the same finder instance. Source list refresh can be
    // triggered by multiple app collectors, so serialize discovery calls.
    std::lock_guard<std::mutex> discovery_call_lock(g_discovery_call_mutex);

    // ensure_ndi_initialized acquires and releases g_ndi_init_mutex internally.
    if (!ensure_ndi_initialized()) {
        return {};
    }

    log_native_info("discover: NDI_DISCOVERY_SERVER=" + discovery_endpoints_csv_snapshot());

    NDIlib_find_instance_t local_finder;
    {
        std::lock_guard<std::mutex> lock(g_finder_mutex);
        if (g_finder == nullptr) {
            NDIlib_find_create_t create_description;
            std::memset(&create_description, 0, sizeof(create_description));
            create_description.show_local_sources = true;
            create_description.p_groups = nullptr;
            create_description.p_extra_ips = nullptr;

            g_finder = NDIlib_find_create_v2(&create_description);
            if (g_finder == nullptr) {
                log_native_info("finder create failed");
                return {};
            }
            log_native_info("NDI finder created (persistent)");
        }
        local_finder = g_finder;
    }

    // Wait outside the lock — blocks up to 5 s on first call, returns early
    // once sources are known to the finder (subsequent ticks are typically fast).
    NDIlib_find_wait_for_sources(local_finder, 5000);

    // Reacquire to safely read results; verify the finder hasn't been replaced.
    std::lock_guard<std::mutex> lock(g_finder_mutex);
    if (g_finder == nullptr || g_finder != local_finder) {
        log_native_info("discover: finder replaced during wait, skipping stale result");
        return {};
    }

    std::vector<DiscoveredSource> discovered;
    append_find_sources(g_finder, discovered);

    // If legacy finder yields nothing, query the NDI 6 sender-listener plane.
    if (discovered.empty()) {
        append_send_listener_sources(discovered);
    }

    log_native_info("discover total=" + std::to_string(discovered.size()));
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

// Stops the receiver thread without holding g_state_mutex during join().
// Holding the mutex while calling join() causes a deadlock: this thread owns
// g_state_mutex and waits for the receiver thread to exit, while the receiver
// thread is blocked trying to acquire g_state_mutex to store a video frame.
void stop_receiver_safe() {
    g_receiver_running = false;          // atomic write, no lock needed
    if (g_receiver_thread.joinable()) {
        g_receiver_thread.join();        // join WITHOUT g_state_mutex held
    }
    // Receiver thread is done; clean up NDI handle and frame buffer.
#ifdef NDI_SDK_AVAILABLE
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        if (g_recv_instance != nullptr) {
            NDIlib_recv_destroy(g_recv_instance);
            g_recv_instance = nullptr;
        }
        clear_latest_frame_locked();
    }
#else
    clear_latest_frame();
#endif
}

void start_receiver_native(const std::string& source_id) {
    // Stop old receiver BEFORE acquiring g_state_mutex to avoid deadlock.
    stop_receiver_safe();

    std::lock_guard<std::mutex> lock(g_state_mutex);
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
    } else if (!discovered_url.empty()) {
        source_to_connect.p_ndi_name = nullptr;
        source_to_connect.p_url_address = discovered_url.c_str();
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
                const auto now = std::chrono::steady_clock::now();
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

                    g_video_frame_times.push_back(now);
                    const auto one_second_ago = now - std::chrono::seconds(1);
                    while (!g_video_frame_times.empty() && g_video_frame_times.front() < one_second_ago) {
                        g_video_frame_times.pop_front();
                    }
                    g_measured_receiver_fps = static_cast<float>(g_video_frame_times.size());
                }

                NDIlib_recv_performance_t total_performance;
                std::memset(&total_performance, 0, sizeof(total_performance));
                NDIlib_recv_performance_t dropped_performance;
                std::memset(&dropped_performance, 0, sizeof(dropped_performance));
                NDIlib_recv_get_performance(g_recv_instance, &total_performance, &dropped_performance);
                {
                    std::lock_guard<std::mutex> lock(g_state_mutex);
                    g_prev_total_video = total_performance.video_frames;
                    g_prev_dropped_video = dropped_performance.video_frames;
                    g_drop_percent_window = total_performance.video_frames == 0
                        ? 0.0f
                        : (static_cast<float>(dropped_performance.video_frames) * 100.0f /
                            static_cast<float>(total_performance.video_frames));
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
    // Do NOT call discover_sources_native() while holding g_state_mutex: the
    // network scan takes up to 3 seconds and would block concurrent frame reads.
    bool needs_discovery = false;
    {
        std::lock_guard<std::mutex> check_lock(g_state_mutex);
        needs_discovery = g_last_discovered_sources.empty();
    }
    if (needs_discovery) {
        auto discovered = discover_sources_native();   // slow scan - NO mutex
        std::lock_guard<std::mutex> store_lock(g_state_mutex);
        if (g_last_discovered_sources.empty()) {
            g_last_discovered_sources = std::move(discovered);
        }
    }
    std::vector<std::string> display_names;
    {
        std::lock_guard<std::mutex> read_lock(g_state_mutex);
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
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSetAppDataDir(
    JNIEnv* env,
    jobject /* this */,
    jstring data_dir
) {
    if (data_dir == nullptr) {
        return;
    }
    const char* raw = env->GetStringUTFChars(data_dir, nullptr);
    if (raw) {
        // Set HOME so that the NDI SDK (Linux-based) resolves ~/.ndi/ndi-config.v1.json
        // to <app-data-dir>/.ndi/ndi-config.v1.json.
        setenv("HOME", raw, 1);
        log_native_info(std::string("HOME set to: ") + raw);
        env->ReleaseStringUTFChars(data_dir, raw);
    }
}

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSetDiscoveryExtraIps(
    JNIEnv* env,
    jobject /* this */,
    jstring extra_ips_csv
) {
#ifdef NDI_SDK_AVAILABLE
    const char* raw_value = extra_ips_csv != nullptr ? env->GetStringUTFChars(extra_ips_csv, nullptr) : nullptr;
    const std::string incoming = raw_value != nullptr ? raw_value : "";
    bool changed = false;
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        if (g_discovery_extra_ips != incoming) {
            g_discovery_extra_ips = incoming;
            g_last_discovered_sources.clear();
            g_discovered_url_by_source_id.clear();
            changed = true;
        }
    }
    if (raw_value != nullptr) {
        env->ReleaseStringUTFChars(extra_ips_csv, raw_value);
    }
    if (!changed) {
        log_native_info("NDI discovery endpoints unchanged; skipping reinit");
        return;
    }
    // Block active discovery loop while reconfiguring NDI/finder/listener state.
    std::lock_guard<std::mutex> discovery_lock(g_discovery_call_mutex);

    // Destroy the persistent finder first so it isn't used against a
    // stale NDI init state. Lock order: g_finder_mutex before g_ndi_init_mutex.
    {
        std::lock_guard<std::mutex> finder_lock(g_finder_mutex);
        if (g_finder != nullptr) {
            NDIlib_find_destroy(g_finder);
            g_finder = nullptr;
            log_native_info("NDI finder destroyed (endpoint changed)");
        }
    }
    {
        std::lock_guard<std::mutex> listener_lock(g_send_listener_mutex);
        destroy_send_listener_locked();
    }
    // Reinitialize NDI with the updated NDI_DISCOVERY_SERVER env var so the
    // SDK picks up the new endpoint. Safe only when no active streams exist.
    {
        std::lock_guard<std::mutex> init_lock(g_ndi_init_mutex);
        const bool active = (g_recv_instance != nullptr || g_send_instance != nullptr);
        if (!active && g_ndi_initialized.load(std::memory_order_relaxed)) {
            NDIlib_destroy();
            g_ndi_initialized.store(false, std::memory_order_release);
            log_native_info("NDI reinit scheduled: discovery endpoint changed");
        }
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
    stop_receiver_safe();
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

extern "C" JNIEXPORT void JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeApplyReceiverQualityProfile(
    JNIEnv* env,
    jobject /* this */,
    jstring /* profile_id */,
    jint max_width,
    jint max_height,
    jint target_fps
) {
#ifdef NDI_SDK_AVAILABLE
    (void)env;
    std::lock_guard<std::mutex> lock(g_state_mutex);
    g_quality_policy.bandwidth_tier = static_cast<int>(NDIlib_recv_bandwidth_highest);
    g_quality_policy.target_width = std::max(0, static_cast<int>(max_width));
    g_quality_policy.target_height = std::max(0, static_cast<int>(max_height));
    g_quality_policy.target_fps = std::max(1, static_cast<int>(target_fps));
    g_quality_policy.codec_pref = "adaptive";
#else
    throw_java_runtime_exception(env, "NDI SDK is not linked in this build");
#endif
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSetFrameRatePolicy(
    JNIEnv* /* env */,
    jobject /* this */,
    jint fps
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    g_quality_policy.target_fps = std::max(1, static_cast<int>(fps));
    return JNI_TRUE;
#else
    (void)fps;
    return JNI_FALSE;
#endif
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeSetResolutionPolicy(
    JNIEnv* /* env */,
    jobject /* this */,
    jint width,
    jint height
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    g_quality_policy.target_width = std::max(0, static_cast<int>(width));
    g_quality_policy.target_height = std::max(0, static_cast<int>(height));
    return JNI_TRUE;
#else
    (void)width;
    (void)height;
    return JNI_FALSE;
#endif
}

extern "C" JNIEXPORT jobjectArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativePerformDiscoveryCheck(
    JNIEnv* env,
    jobject /* this */,
    jstring host,
    jint port,
    jstring /* correlation_id */
) {
    const char* raw_host = host != nullptr ? env->GetStringUTFChars(host, nullptr) : nullptr;
    const std::string target_host = raw_host != nullptr ? raw_host : "";
    if (raw_host != nullptr) {
        env->ReleaseStringUTFChars(host, raw_host);
    }

    if (target_host.empty() || port <= 0) {
        return to_java_string_array(env, {"false", "UNKNOWN", "Invalid discovery endpoint"});
    }

#ifdef NDI_SDK_AVAILABLE
    if (!ensure_ndi_initialized()) {
        return to_java_string_array(env, {"false", "HANDSHAKE_FAILED", "NDI SDK initialization failed"});
    }

    NDIlib_find_create_t create_description;
    std::memset(&create_description, 0, sizeof(create_description));
    create_description.show_local_sources = true;
    create_description.p_groups = nullptr;
    create_description.p_extra_ips = nullptr;

    NDIlib_find_instance_t probe_finder = NDIlib_find_create_v2(&create_description);
    if (probe_finder == nullptr) {
        return to_java_string_array(env, {"false", "HANDSHAKE_FAILED", "Unable to create NDI finder for discovery check"});
    }

    NDIlib_find_wait_for_sources(probe_finder, 1200);
    NDIlib_find_destroy(probe_finder);
    return to_java_string_array(env, {"true", "NONE", ""});
#else
    (void)target_host;
    return to_java_string_array(env, {"false", "UNKNOWN", "NDI SDK is not linked in this build"});
#endif
}

extern "C" JNIEXPORT jfloat JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetReceiverDroppedFramePercent(
    JNIEnv* /* env */,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    return g_drop_percent_window;
#else
    return 0.0f;
#endif
}

extern "C" JNIEXPORT jintArray JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetActualResolution(
    JNIEnv* env,
    jobject /* this */
) {
    jint values[2] = {0, 0};
    {
        std::lock_guard<std::mutex> lock(g_state_mutex);
        values[0] = g_latest_frame_width;
        values[1] = g_latest_frame_height;
    }
    jintArray result = env->NewIntArray(2);
    env->SetIntArrayRegion(result, 0, 2, values);
    return result;
}

extern "C" JNIEXPORT jfloat JNICALL
Java_com_ndi_sdkbridge_NativeNdiBridge_nativeGetMeasuredReceiverFps(
    JNIEnv* /* env */,
    jobject /* this */
) {
#ifdef NDI_SDK_AVAILABLE
    std::lock_guard<std::mutex> lock(g_state_mutex);
    return g_measured_receiver_fps;
#else
    return 0.0f;
#endif
}

