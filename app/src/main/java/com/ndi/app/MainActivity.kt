package com.ndi.app

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.isVisible
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.NavHostFragment
import com.google.android.material.bottomnavigation.BottomNavigationView
import com.google.android.material.navigationrail.NavigationRailView
import com.ndi.app.databinding.ActivityMainBinding
import com.ndi.app.di.AppGraph
import com.ndi.app.navigation.LaunchContextResolver
import com.ndi.app.navigation.TopLevelNavEvent
import com.ndi.app.navigation.TopLevelNavViewModel
import com.ndi.app.navigation.TopLevelNavigationCoordinator
import com.ndi.app.navigation.TopLevelNavigationHost
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.feature.ndibrowser.home.HomeNavigationCallback
import com.ndi.feature.ndibrowser.home.HomeNavigationEvent
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity(), HomeNavigationCallback {

    private lateinit var binding: ActivityMainBinding
    private lateinit var navViewModel: TopLevelNavViewModel
    private lateinit var navHost: TopLevelNavigationHost

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val appGraph = AppGraph.initialize(applicationContext)
        AppGraph.initialize(applicationContext)

        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // Initialize ViewModel
        navViewModel = ViewModelProvider(
            this,
            TopLevelNavViewModel.Factory(
                navigationRepository = appGraph.topLevelNavigationRepository,
            ),
        )[TopLevelNavViewModel::class.java]

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
            }
        }

        // Trigger initial navigation based on launch context
        if (savedInstanceState == null) {
            val launchContext = LaunchContextResolver.resolve(intent)
            navViewModel.onAppLaunch(launchContext)
        }
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
            is TopLevelNavEvent.NavigationFailure -> Unit // already emitted to telemetry
        }
    }
}
