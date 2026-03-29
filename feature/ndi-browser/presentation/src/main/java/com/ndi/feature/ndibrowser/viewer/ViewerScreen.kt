package com.ndi.feature.ndibrowser.viewer

import android.graphics.BitmapFactory
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.view.LayoutInflater
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.ArrayAdapter
import android.widget.LinearLayout
import android.widget.ListView
import android.widget.TextView
import android.widget.PopupWindow
import androidx.core.view.isVisible
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.findNavController
import com.ndi.core.model.PlaybackState
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentViewerBinding
import com.ndi.feature.ndibrowser.settings.DeveloperOverlayRenderer
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import java.net.HttpURLConnection
import java.net.URL
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import android.widget.FrameLayout
import androidx.core.view.doOnLayout

class ViewerFragment : Fragment() {

    private var binding: FragmentViewerBinding? = null
    private var relayPreviewJob: Job? = null
    private var relayPreviewSourceId: String? = null
    private var disconnectDialog: androidx.appcompat.app.AlertDialog? = null
    private var qualityPopupWindow: PopupWindow? = null
    private var streamAspectRatio: Float = 16f / 9f
    private var activeQualityProfileId: String = "smooth"
    private var latestStreamWidth: Int = 0
    private var latestStreamHeight: Int = 0
    private var latestScaledDimensions: ScaledDimensions? = null

    private val viewModel: ViewerViewModel by viewModels {
        ViewerViewModel.Factory(
            ViewerDependencies.requireViewerRepository(),
            ViewerDependencies.requireUserSelectionRepository(),
        )
    }

    private val scalingViewModel: PlayerScalingViewModel by viewModels()

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentViewerBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        fragmentBinding.retryButton.setOnClickListener { viewModel.onRetryPressed() }
        fragmentBinding.backToListButton.setOnClickListener {
            viewLifecycleOwner.lifecycleScope.launch {
                viewModel.stopViewingForBackNavigation()
                runCatching { findNavController().popBackStack() }
            }
        }
        fragmentBinding.qualityButton.setOnClickListener {
            showQualityPresetPopup(fragmentBinding.qualityButton)
        }
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewModel.onLayoutMeasured(resources.configuration.screenWidthDp)
        viewModel.onViewerOpened(arguments?.getString("sourceId").orEmpty())

        binding?.viewerSurfacePlaceholder?.doOnLayout { container ->
            scalingViewModel.updatePlayerBounds(
                width = container.width,
                height = container.height,
                streamAspectRatio = streamAspectRatio,
                orientation = if (resources.configuration.orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE) {
                    Orientation.LANDSCAPE
                } else {
                    Orientation.PORTRAIT
                },
            )
        }

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                scalingViewModel.scaledDimensions.collect { scaled ->
                    val image = binding?.viewerPreviewImage
                        ?: return@collect
                    if (scaled == null) return@collect
                    latestScaledDimensions = scaled
                    applyPreviewLayout(image)
                }
            }
        }

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                val overlayFlow = ViewerDependencies.overlayStateFlowOrNull()
                val stateFlow = if (overlayFlow == null) {
                    viewModel.uiState
                } else {
                    viewModel.uiState.combine(overlayFlow) { state, overlayDisplayState ->
                        state.copy(overlayDisplayState = overlayDisplayState)
                    }
                }

                stateFlow.collect { state ->
                    val fragmentBinding = binding ?: return@collect
                    fragmentBinding.viewerTitle.text = getString(R.string.ndi_viewer_title, state.sourceId)
                    fragmentBinding.viewerState.text = if (state.isUnavailableRestore) {
                        getString(
                            R.string.ndi_viewer_restore_unavailable_state,
                            state.sourceId,
                        )
                    } else {
                        getString(
                            R.string.ndi_viewer_quality_state,
                            state.playbackState.name,
                            state.activeQualityProfileId,
                            state.droppedFramePercent,
                        )
                    }
                    activeQualityProfileId = state.activeQualityProfileId
                    latestStreamWidth = state.streamWidth
                    latestStreamHeight = state.streamHeight
                    binding?.viewerPreviewImage?.let { image ->
                        applyPreviewLayout(image)
                    }
                    fragmentBinding.qualityButton.text = getString(
                        R.string.ndi_viewer_quality_button_selected,
                        profileDisplayName(state.activeQualityProfileId),
                    )
                    fragmentBinding.tabletBadge.isVisible = state.layoutMode == ViewerLayoutMode.TABLET
                    fragmentBinding.recoveryMessage.text = state.interruptionMessage
                    fragmentBinding.recoveryMessage.isVisible = state.interruptionMessage != null
                    fragmentBinding.retryButton.isVisible = state.recoveryActionsVisible

                    if (state.streamWidth > 0 && state.streamHeight > 0) {
                        streamAspectRatio = state.streamWidth.toFloat() / state.streamHeight.toFloat()
                        val container = fragmentBinding.viewerSurfacePlaceholder
                        if (container.width > 0 && container.height > 0) {
                            scalingViewModel.updatePlayerBounds(
                                width = container.width,
                                height = container.height,
                                streamAspectRatio = streamAspectRatio,
                                orientation = if (resources.configuration.orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE) {
                                    Orientation.LANDSCAPE
                                } else {
                                    Orientation.PORTRAIT
                                },
                            )
                        }
                    }
                    DeveloperOverlayRenderer.render(
                        container = fragmentBinding.developerOverlay.developerOverlayContainer,
                        streamStatusView = fragmentBinding.developerOverlay.overlayStreamStatus,
                        sessionIdView = fragmentBinding.developerOverlay.overlaySessionId,
                        recentLogsView = fragmentBinding.developerOverlay.overlayRecentLogs,
                        overlayDisplayState = state.overlayDisplayState,
                    )
                    renderRelayPreview(
                        sourceId = state.sourceId,
                        playbackState = state.playbackState,
                        restoredPreviewPath = state.restoredPreviewPath,
                        isUnavailableRestore = state.isUnavailableRestore,
                    )
                    renderDisconnectionDialog(state)
                }
            }
        }
    }

    private fun showQualityPresetPopup(anchor: View) {
        if (!isAdded) return
        qualityPopupWindow?.dismiss()

        val context = requireContext()
        val items = QualitySettingsMenuComposable.buildItems(requireContext())

        val labels = items.map { item ->
            val selected = item.profile.profileId == viewModel.uiState.value.activeQualityProfileId
            val prefix = if (selected) "\u2713 " else ""
            "$prefix${item.title} - ${item.hint}"
        }

        val listView = ListView(context).apply {
            adapter = object : ArrayAdapter<String>(context, android.R.layout.simple_list_item_1, labels) {
                override fun getView(position: Int, convertView: View?, parent: ViewGroup): View {
                    val view = super.getView(position, convertView, parent)
                    (view as? TextView)?.setTextColor(Color.WHITE)
                    return view
                }
            }
            setBackgroundColor(Color.TRANSPARENT)
            dividerHeight = 0
            setOnItemClickListener { _, _, index, _ ->
                val selected = items.getOrNull(index) ?: return@setOnItemClickListener
                viewModel.onQualityProfileSelected(selected.profile.profileId)
                qualityPopupWindow?.dismiss()
            }
        }

        val titleView = TextView(context).apply {
            text = getString(R.string.ndi_viewer_quality_sheet_title)
            setTextColor(Color.WHITE)
            setPadding(dpToPx(8), dpToPx(6), dpToPx(8), dpToPx(4))
            setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_TitleMedium)
        }

        val popupWidth = maxOf(dpToPx(140), (resources.displayMetrics.widthPixels * 0.34f).toInt())
        listView.layoutParams = LinearLayout.LayoutParams(
            popupWidth - dpToPx(16),
            ViewGroup.LayoutParams.WRAP_CONTENT,
        )

        val content = LinearLayout(context).apply {
            orientation = LinearLayout.VERTICAL
            val bg = GradientDrawable().apply {
                setColor(Color.parseColor("#CC1E1E1E")) // ~80% opacity
                cornerRadius = dpToPx(12).toFloat()
            }
            background = bg
            elevation = dpToPx(8).toFloat()
            setPadding(dpToPx(8), dpToPx(8), dpToPx(8), dpToPx(8))
            addView(titleView)
            addView(listView)
        }

        content.measure(
            View.MeasureSpec.makeMeasureSpec(popupWidth, View.MeasureSpec.EXACTLY),
            View.MeasureSpec.makeMeasureSpec(resources.displayMetrics.heightPixels, View.MeasureSpec.AT_MOST),
        )

        val popupHeight = content.measuredHeight
        val yOffset = -(popupHeight + anchor.height + dpToPx(4))

        qualityPopupWindow = PopupWindow(
            content,
            popupWidth,
            ViewGroup.LayoutParams.WRAP_CONTENT,
            true,
        ).apply {
            isOutsideTouchable = true
            elevation = dpToPx(8).toFloat()
            setOnDismissListener { qualityPopupWindow = null }
            showAsDropDown(anchor, 0, yOffset, Gravity.END)
        }
    }

    private fun profileDisplayName(profileId: String): String {
        val items = QualitySettingsMenuComposable.buildItems(requireContext())
        return items.firstOrNull { it.profile.profileId == profileId }?.title
            ?: getString(R.string.ndi_viewer_quality_smooth)
    }

    private fun applyPreviewLayout(image: android.widget.ImageView) {
        val scaled = latestScaledDimensions ?: return
        val params = (image.layoutParams as FrameLayout.LayoutParams)

        val streamW = latestStreamWidth
        val streamH = latestStreamHeight
        params.width = if (streamW > 0) minOf(scaled.width, streamW) else scaled.width
        params.height = if (streamH > 0) minOf(scaled.height, streamH) else scaled.height
        params.gravity = android.view.Gravity.CENTER
        image.layoutParams = params
    }

    private fun dpToPx(dp: Int): Int {
        return (dp * resources.displayMetrics.density).toInt()
    }

    private fun renderDisconnectionDialog(state: ViewerUiState) {
        if (!state.disconnectionDialogVisible) {
            disconnectDialog?.dismiss()
            disconnectDialog = null
            return
        }

        if (disconnectDialog?.isShowing == true) {
            disconnectDialog?.setMessage(
                getString(
                    R.string.ndi_viewer_reconnecting_message,
                    state.recoveryElapsedSeconds,
                ),
            )
            disconnectDialog?.getButton(androidx.appcompat.app.AlertDialog.BUTTON_POSITIVE)
                ?.isEnabled = state.manualReconnectVisible
            return
        }

        disconnectDialog = MaterialAlertDialogBuilder(requireContext())
            .setTitle(getString(R.string.ndi_viewer_stream_disconnected_title))
            .setMessage(
                getString(
                    R.string.ndi_viewer_reconnecting_message,
                    state.recoveryElapsedSeconds,
                ),
            )
            .setCancelable(true)
            .setOnDismissListener {
                viewModel.onDisconnectionDialogDismissed()
                disconnectDialog = null
            }
            .setPositiveButton(R.string.ndi_viewer_manual_reconnect) { _, _ ->
                viewModel.onRetryPressed()
            }
            .setNegativeButton(R.string.ndi_viewer_cancel) { _, _ ->
                viewLifecycleOwner.lifecycleScope.launch {
                    viewModel.stopViewingForBackNavigation()
                    runCatching { findNavController().popBackStack() }
                }
            }
            .show()
        disconnectDialog?.getButton(androidx.appcompat.app.AlertDialog.BUTTON_POSITIVE)
            ?.isEnabled = state.manualReconnectVisible
    }

    private fun renderRelayPreview(
        sourceId: String,
        playbackState: PlaybackState,
        restoredPreviewPath: String?,
        isUnavailableRestore: Boolean,
    ) {
        val fragmentBinding = binding ?: return
        if (isUnavailableRestore) {
            relayPreviewJob?.cancel()
            relayPreviewJob = null
            relayPreviewSourceId = null
            val restoreBitmap = restoredPreviewPath
                ?.takeIf { path -> java.io.File(path).exists() }
                ?.let(BitmapFactory::decodeFile)
            fragmentBinding.viewerPreviewImage.setImageBitmap(restoreBitmap)
            return
        }

        val canRenderPreview = playbackState == PlaybackState.PLAYING
        if (!canRenderPreview) {
            relayPreviewJob?.cancel()
            relayPreviewJob = null
            relayPreviewSourceId = null
            fragmentBinding.viewerPreviewImage.setImageDrawable(null)
            return
        }

        if (relayPreviewJob != null && relayPreviewSourceId == sourceId) {
            return
        }

        relayPreviewJob?.cancel()
        relayPreviewSourceId = sourceId
        relayPreviewJob = viewLifecycleOwner.lifecycleScope.launch {
            while (isActive) {
                val bitmap = if (sourceId.startsWith("relay-screen:")) {
                    withContext(Dispatchers.IO) {
                        runCatching {
                            val encodedSourceId = java.net.URLEncoder.encode(sourceId, Charsets.UTF_8.name())
                            val url = URL("http://localhost:17455/frame/$encodedSourceId")
                            val connection = (url.openConnection() as HttpURLConnection).apply {
                                requestMethod = "GET"
                                connectTimeout = 1_000
                                readTimeout = 1_500
                            }
                            try {
                                if (connection.responseCode !in 200..299) return@runCatching null
                                connection.inputStream.use { input -> BitmapFactory.decodeStream(input) }
                            } finally {
                                connection.disconnect()
                            }
                        }.getOrNull()
                    }
                } else {
                    // getLatestVideoFrame() calls native JNI methods (nativeGetLatestReceiverFrame*).
                    // Running on the main thread causes ANR when stopReceiver() concurrently holds the
                    // native NDI mutex on an IO thread. Always fetch frames on the IO dispatcher.
                    withContext(Dispatchers.IO) {
                        val frame = viewModel.getLatestVideoFrame()
                        if (frame == null) {
                            null
                        } else {
                            runCatching {
                                android.graphics.Bitmap.createBitmap(
                                    frame.argbPixels,
                                    frame.width,
                                    frame.height,
                                    android.graphics.Bitmap.Config.ARGB_8888,
                                )
                            }.getOrNull()
                        }
                    }
                }

                if (bitmap != null) {
                    val displayBitmap = applyProfileRenderPolicy(bitmap, activeQualityProfileId)
                    streamAspectRatio = bitmap.width.toFloat() / bitmap.height.toFloat()
                    val container = binding?.viewerSurfacePlaceholder
                    if (container != null && container.width > 0 && container.height > 0) {
                        scalingViewModel.updatePlayerBounds(
                            width = container.width,
                            height = container.height,
                            streamAspectRatio = streamAspectRatio,
                            orientation = if (resources.configuration.orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE) {
                                Orientation.LANDSCAPE
                            } else {
                                Orientation.PORTRAIT
                            },
                        )
                    }
                    binding?.viewerPreviewImage?.setImageBitmap(displayBitmap)
                } else {
                    // Clear stale frame immediately when publisher has stopped or no relay frame is available.
                    binding?.viewerPreviewImage?.setImageDrawable(null)
                }
                delay(framePollDelayMillis(activeQualityProfileId))
            }
        }
    }

    private fun framePollDelayMillis(profileId: String): Long {
        return when (profileId) {
            "high_quality" -> 34L
            "balanced" -> 42L
            else -> 25L
        }
    }

    private fun applyProfileRenderPolicy(
        bitmap: android.graphics.Bitmap,
        profileId: String,
    ): android.graphics.Bitmap {
        val target = when (profileId) {
            "smooth" -> 640 to 360
            "balanced" -> 1280 to 720
            else -> null
        } ?: return bitmap

        val maxWidth = target.first
        val maxHeight = target.second
        if (bitmap.width <= maxWidth && bitmap.height <= maxHeight) {
            return bitmap
        }

        val widthScale = maxWidth.toFloat() / bitmap.width.toFloat()
        val heightScale = maxHeight.toFloat() / bitmap.height.toFloat()
        val scale = minOf(widthScale, heightScale)
        val scaledWidth = (bitmap.width * scale).toInt().coerceAtLeast(1)
        val scaledHeight = (bitmap.height * scale).toInt().coerceAtLeast(1)
        return android.graphics.Bitmap.createScaledBitmap(bitmap, scaledWidth, scaledHeight, true)
    }

    override fun onDestroyView() {
        qualityPopupWindow?.dismiss()
        qualityPopupWindow = null
        disconnectDialog?.dismiss()
        disconnectDialog = null
        relayPreviewJob?.cancel()
        relayPreviewJob = null
        relayPreviewSourceId = null
        binding = null
        super.onDestroyView()
    }

    override fun onStart() {
        super.onStart()
        viewModel.onStart()
    }

    override fun onStop() {
        viewModel.onStop()
        super.onStop()
    }

    override fun onResume() {
        super.onResume()
    }
}
