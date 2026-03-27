package com.ndi.feature.ndibrowser.output

import android.app.Activity
import android.content.Context
import android.media.projection.MediaProjectionManager
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.activity.result.contract.ActivityResultContracts
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentOutputControlBinding
import com.ndi.sdkbridge.NativeNdiBridge
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

class OutputControlFragment : Fragment() {

    private var binding: FragmentOutputControlBinding? = null

    private val viewModel: OutputControlViewModel by viewModels {
        OutputControlViewModel.Factory(
            OutputDependencies.requireOutputRepository(),
            OutputDependencies.requireOutputConfigurationRepository(),
            OutputDependencies.requireScreenCaptureConsentRepository(),
        )
    }

    private val screenCaptureLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult(),
    ) { result ->
        val tokenRef = if (result.resultCode == Activity.RESULT_OK) {
            NativeNdiBridge.registerScreenCapturePermissionResult(result.resultCode, result.data)
        } else {
            null
        }
        val granted = tokenRef != null
        viewModel.onScreenCaptureConsentResult(granted = granted, tokenRef = tokenRef)
    }

    private lateinit var screen: OutputControlScreen

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentOutputControlBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        screen = OutputControlScreen(
            binding = fragmentBinding,
            onStartPressed = {
                viewModel.onStreamNameChanged(screen.currentStreamName())
                viewModel.onStartOutputPressed()
            },
            onStopPressed = {
                viewModel.onStopOutputPressed()
            },
            onRetryPressed = {
                viewModel.onRetryOutputPressed()
            },
        )
        fragmentBinding.outputTopAppBar.inflateMenu(R.menu.output_menu)
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewModel.onOutputScreenVisible(arguments?.getString("sourceId").orEmpty())

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch {
                    val overlayFlow = OutputDependencies.overlayStateFlowOrNull()
                    val stateFlow = if (overlayFlow == null) {
                        viewModel.uiState
                    } else {
                        viewModel.uiState.combine(overlayFlow) { state, overlayDisplayState ->
                            state.copy(overlayDisplayState = overlayDisplayState)
                        }
                    }
                    stateFlow.collect { state ->
                        binding ?: return@collect
                        screen.render(state)
                    }
                }
                launch {
                    viewModel.consentPromptEvents.collect {
                        val projectionManager = requireContext().getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
                        screenCaptureLauncher.launch(projectionManager.createScreenCaptureIntent())
                    }
                }
            }
        }
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }

    override fun onResume() {
        super.onResume()
    }
}
