package com.ndi.app

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.navigation.fragment.NavHostFragment
import com.ndi.app.databinding.ActivityMainBinding
import com.ndi.app.di.AppGraph

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        AppGraph.initialize(applicationContext)

        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        if (savedInstanceState == null) {
            val navHostFragment = NavHostFragment.create(R.navigation.main_nav_graph)
            supportFragmentManager
                .beginTransaction()
                .replace(R.id.nav_host_container, navHostFragment)
                .setPrimaryNavigationFragment(navHostFragment)
                .commitNow()
        }
    }
}
