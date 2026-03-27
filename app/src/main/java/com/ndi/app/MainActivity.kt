package com.ndi.app

import android.os.Bundle
import androidx.activity.addCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.isVisible
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.NavHostFragment
import com.google.android.material.bottomnavigation.BottomNavigationView
import com.google.android.material.navigationrail.NavigationRailView
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import com.ndi.app.databinding.ActivityMainBinding
import com.ndi.app.di.AppGraph
import com.ndi.app.navigation.LaunchContextResolver
import com.ndi.app.navigation.AppContinuityViewModel
import com.ndi.app.navigation.TopLevelNavEvent
import com.ndi.app.navigation.TopLevelNavViewModel
import com.ndi.app.navigation.TopLevelNavigationCoordinator
import com.ndi.app.navigation.TopLevelNavigationHost
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.ndibrowser.home.HomeNavigationCallback
import com.ndi.feature.ndibrowser.home.HomeNavigationEvent
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity(), HomeNavigationCallback {

    private lateinit var appGraph: AppGraph
    private lateinit var binding: ActivityMainBinding
    private var appliedAccentColorId: String? = null
    private lateinit var navViewModel: TopLevelNavViewModel
    private lateinit var continuityViewModel: AppContinuityViewModel
    private lateinit var navHost: TopLevelNavigationHost

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        appGraph = AppGraph.initialize(applicationContext)

        // Read accent synchronously (single-row DB read) and apply M3 theme overlay
        // BEFORE view inflation so every Material3 component picks up colorPrimary etc.
        appliedAccentColorId = runBlocking {
            withContext(Dispatchers.IO) {
                appGraph.themeEditorRepository.getThemePreference().accentColorId
            }
        }
        theme.applyStyle(accentThemeOverlayResId(appliedAccentColorId), true)

        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // Initialize ViewModel
        navViewModel = ViewModelProvider(
            this,
            TopLevelNavViewModel.Factory(
                navigationRepository = appGraph.topLevelNavigationRepository,
            ),
        )[TopLevelNavViewModel::class.java]
        continuityViewModel = ViewModelProvider(
            this,
            AppContinuityViewModel.Factory(
                streamContinuityRepository = appGraph.streamContinuityRepository,
            ),
        )[AppContinuityViewModel::class.java]

        // Measure layout for adaptive nav
        binding.root.post {
            val widthDp = resources.displayMetrics.run { widthPixels / density }.toInt()
            navViewModel.onScreenWidthChanged(widthDp)
        }

        // Wire navigation controls
        val bottomNav: BottomNavigationView? = binding.root.findViewById(R.id.bottom_navigation)
        val navRail: NavigationRailView? = binding.root.findViewById(R.id.nav_rail)

        val navHostFragment = supportFragmentManager
            .findFragmentById(R.id.nav_host_container) as? NavHostFragment
            ?: NavHostFragment.create(R.navigation.main_nav_graph).also {
                supportFragmentManager.beginTransaction()
                    .replace(R.id.nav_host_container, it)
                    .setPrimaryNavigationFragment(it)
                    .commitNow()
            }

        navHost = TopLevelNavigationHost(
            navController = navHostFragment.navController,
            coordinator = TopLevelNavigationCoordinator(),
            onSelectedItemChanged = { destId ->
                bottomNav?.selectedItemId = destId
                navRail?.selectedItemId = destId
            },
        )

        navHostFragment.navController.addOnDestinationChangedListener { _, destination, _ ->
            val selectedTopLevel = when (destination.id) {
                R.id.homeDashboardFragment -> TopLevelDestination.HOME
                R.id.streamFragment,
                R.id.outputControlFragment,
                -> TopLevelDestination.STREAM
                R.id.viewFragment,
                R.id.viewerHostFragment,
                -> TopLevelDestination.VIEW
                R.id.settingsFragment -> TopLevelDestination.SETTINGS
                else -> null
            }

            selectedTopLevel?.let {
                navViewModel.onNavDestinationObserved(it)
                navHost.syncSelectedItem(it)
            }
        }

        bottomNav?.setOnItemSelectedListener { item ->
            navHost.resolveDestination(item.itemId)?.let { dest ->
                navViewModel.onDestinationSelected(dest, NavigationTrigger.BOTTOM_NAV)
            }
            true
        }

        navRail?.setOnItemSelectedListener { item ->
            navHost.resolveDestination(item.itemId)?.let { dest ->
                navViewModel.onDestinationSelected(dest, NavigationTrigger.NAV_RAIL)
            }
            true
        }

        // Observe navigation UI state
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch {
                    navViewModel.uiState.collect { state ->
                        val isTablet = state.navLayoutProfile == NavigationLayoutProfile.TABLET_NAV_RAIL
                        bottomNav?.isVisible = !isTablet
                        navRail?.isVisible = isTablet
                    }
                }

                launch {
                    navViewModel.events.collect { event ->
                        handleNavEvent(event)
                    }
                }

                launch {
                    // When the accent changes after the initial view inflation, recreate
                    // the activity so all Material3 components inherit the new colorPrimary.
                    appGraph.appThemeCoordinator.activeAccentColorId.collect { accentColorId ->
                        if (accentColorId != null && accentColorId != appliedAccentColorId) {
                            appliedAccentColorId = accentColorId
                            recreate()
                        }
                    }
                }
            }
        }

        // Trigger initial navigation based on launch context
        if (savedInstanceState == null) {
            val launchContext = LaunchContextResolver.resolve(intent)
            navViewModel.onAppLaunch(launchContext)
        }

        onBackPressedDispatcher.addCallback(this) {
            val currentDestinationId = navHostFragment.navController.currentDestination?.id
            when (currentDestinationId) {
                R.id.viewerHostFragment -> {
                    // Use the predefined nav-graph action to pop only the viewer,
                    // leaving the source-list fragment intact.  This avoids the
                    // popUpTo(home) + restoreState path that can race with
                    // ViewerFragment teardown and crash.
                    val popped = runCatching {
                        navHostFragment.navController.navigate(
                            R.id.action_viewerHostFragment_to_viewFragment,
                        )
                        true
                    }.getOrDefault(false)
                    if (!popped) {
                        // Fallback: let the system handle it.
                        isEnabled = false
                        onBackPressedDispatcher.onBackPressed()
                        isEnabled = true
                    }
                    // Keep ViewModel in sync; no nav event needed since we handled navigation.
                    navViewModel.onNavDestinationObserved(TopLevelDestination.VIEW)
                }
                R.id.viewFragment -> {
                    val consumed = navViewModel.onBackPressed(
                        currentTopLevelDestination = TopLevelDestination.VIEW,
                        isViewerVisible = false,
                    )
                    if (!consumed) {
                        isEnabled = false
                        onBackPressedDispatcher.onBackPressed()
                        isEnabled = true
                    }
                }
                else -> {
                    isEnabled = false
                    onBackPressedDispatcher.onBackPressed()
                    isEnabled = true
                }
            }
        }
    }

    override fun onStart() {
        super.onStart()
        appGraph.appThemeCoordinator.start()
        continuityViewModel.onAppForegrounded()
    }

    override fun onStop() {
        appGraph.appThemeCoordinator.stop()
        continuityViewModel.onAppBackgrounded(isChangingConfigurations)
        super.onStop()
    }

    override fun onHomeNavigationEvent(event: HomeNavigationEvent) {
        when (event) {
            is HomeNavigationEvent.OpenStream ->
                navViewModel.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.HOME_ACTION)
            is HomeNavigationEvent.OpenView ->
                navViewModel.onDestinationSelected(TopLevelDestination.VIEW, NavigationTrigger.HOME_ACTION)
        }
    }

    private fun handleNavEvent(event: TopLevelNavEvent) {
        when (event) {
            is TopLevelNavEvent.NavigateToHome ->
                navHost.navigateTo(TopLevelDestination.HOME, NavigationTrigger.SYSTEM_RESTORE)
            is TopLevelNavEvent.NavigateToStream ->
                navHost.navigateTo(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            is TopLevelNavEvent.NavigateToView ->
                navHost.navigateTo(TopLevelDestination.VIEW, NavigationTrigger.BOTTOM_NAV)
            is TopLevelNavEvent.NavigateToSettings ->
                navHost.navigateTo(TopLevelDestination.SETTINGS, NavigationTrigger.BOTTOM_NAV)
            is TopLevelNavEvent.NavigationFailure -> Unit // already emitted to telemetry
        }
    }

    private fun accentThemeOverlayResId(accentColorId: String?): Int = when (accentColorId) {
        ThemeAccentPalette.ACCENT_BLUE -> R.style.ThemeOverlay_NdiApp_AccentBlue
        ThemeAccentPalette.ACCENT_GREEN -> R.style.ThemeOverlay_NdiApp_AccentGreen
        ThemeAccentPalette.ACCENT_ORANGE -> R.style.ThemeOverlay_NdiApp_AccentOrange
        ThemeAccentPalette.ACCENT_RED -> R.style.ThemeOverlay_NdiApp_AccentRed
        ThemeAccentPalette.ACCENT_PINK -> R.style.ThemeOverlay_NdiApp_AccentPink
        else -> R.style.ThemeOverlay_NdiApp_AccentTeal
    }
}
