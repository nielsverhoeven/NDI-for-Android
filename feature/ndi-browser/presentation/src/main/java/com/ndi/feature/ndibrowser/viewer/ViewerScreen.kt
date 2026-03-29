package com.ndi.feature.ndibrowser.viewer

import android.graphics.BitmapFactory
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
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
    private var streamAspectRatio: Float = 16f / 9f

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
            viewModel.onBackToListPressed()
            runCatching { findNavController().popBackStack() }
        }
        fragmentBinding.viewerTopAppBar.inflateMenu(R.menu.viewer_menu)
        fragmentBinding.viewerTopAppBar.setOnMenuItemClickListener { item ->
            if (item.itemId == R.id.viewer_menu_quality_presets) {
                showQualityPresetMenu(fragmentBinding.viewerTopAppBar)
                true
            } else {
                false
            }
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
                    val image = binding?.viewerPreviewImage ?: return@collect
                    if (scaled == null) return@collect
                    val params = (image.layoutParams as FrameLayout.LayoutParams)
                    params.width = scaled.width
                    params.height = scaled.height
                    params.gravity = android.view.Gravity.CENTER
                    image.layoutParams = params
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
                    fragmentBinding.viewerState.text = getString(
                        R.string.ndi_viewer_quality_state,
                        state.playbackState.name,
                        state.activeQualityProfileId,
                        state.droppedFramePercent,
                    )
                    fragmentBinding.tabletBadge.isVisible = state.layoutMode == ViewerLayoutMode.TABLET
                    fragmentBinding.recoveryMessage.text = state.interruptionMessage
                    fragmentBinding.recoveryMessage.isVisible = state.interruptionMessage != null
                    fragmentBinding.retryButton.isVisible = state.recoveryActionsVisible
                    DeveloperOverlayRenderer.render(
                        container = fragmentBinding.developerOverlay.developerOverlayContainer,
                        streamStatusView = fragmentBinding.developerOverlay.overlayStreamStatus,
                        sessionIdView = fragmentBinding.developerOverlay.overlaySessionId,
                        recentLogsView = fragmentBinding.developerOverlay.overlayRecentLogs,
                        overlayDisplayState = state.overlayDisplayState,
                    )
                    renderRelayPreview(state.sourceId, state.playbackState)
                    renderDisconnectionDialog(state)
                }
            }
        }
    }

    private fun showQualityPresetMenu(anchor: View) {
        val popup = androidx.appcompat.widget.PopupMenu(requireContext(), anchor)
        val items = QualitySettingsMenuComposable.buildItems(requireContext())
        items.forEachIndexed { index, item ->
            popup.menu.add(0, index, index, "${item.title} - ${item.hint}")
        }
        popup.setOnMenuItemClickListener { menuItem ->
            val index = menuItem.itemId
            val selected = items.getOrNull(index) ?: return@setOnMenuItemClickListener false
            viewModel.onQualityProfileSelected(selected.profile.profileId)
            true
        }
        popup.show()
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
                viewModel.onBackToListPressed()
                runCatching { findNavController().popBackStack() }
            }
            .show()
        disconnectDialog?.getButton(androidx.appcompat.app.AlertDialog.BUTTON_POSITIVE)
            ?.isEnabled = state.manualReconnectVisible
    }

    private fun renderRelayPreview(sourceId: String, playbackState: PlaybackState) {
        val fragmentBinding = binding ?: return
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
                    binding?.viewerPreviewImage?.setImageBitmap(bitmap)
                } else {
                    // Clear stale frame immediately when publisher has stopped or no relay frame is available.
                    binding?.viewerPreviewImage?.setImageDrawable(null)
                }
                delay(750)
            }
        }
    }

    override fun onDestroyView() {
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
