package com.ndi.feature.ndibrowser.settings

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.ndi.core.model.SettingsCategory
import com.ndi.feature.ndibrowser.presentation.databinding.ItemSettingsCategoryBinding

class SettingsCategoryAdapter(
    private val onCategorySelected: (String) -> Unit,
) : RecyclerView.Adapter<SettingsCategoryAdapter.SettingsCategoryViewHolder>() {

    private var categories: List<SettingsCategory> = emptyList()

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SettingsCategoryViewHolder {
        val binding = ItemSettingsCategoryBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return SettingsCategoryViewHolder(binding, onCategorySelected)
    }

    override fun onBindViewHolder(holder: SettingsCategoryViewHolder, position: Int) {
        holder.bind(categories[position])
    }

    override fun getItemCount(): Int = categories.size

    fun submitCategories(items: List<SettingsCategory>) {
        categories = items
        notifyDataSetChanged()
    }

    class SettingsCategoryViewHolder(
        private val binding: ItemSettingsCategoryBinding,
        private val onCategorySelected: (String) -> Unit,
    ) : RecyclerView.ViewHolder(binding.root) {

        fun bind(category: SettingsCategory) {
            binding.settingsCategoryCard.isChecked = category.isSelected
            binding.settingsCategoryTitle.text = category.title
            binding.settingsCategorySubtitle.text = category.subtitle.orEmpty()
            binding.root.id = when (category.id) {
                SettingsViewModel.CATEGORY_APPEARANCE -> com.ndi.feature.ndibrowser.presentation.R.id.settingsCategoryAppearance
                SettingsViewModel.CATEGORY_DISCOVERY -> com.ndi.feature.ndibrowser.presentation.R.id.settingsCategoryDiscovery
                SettingsViewModel.CATEGORY_DEVELOPER -> com.ndi.feature.ndibrowser.presentation.R.id.settingsCategoryDeveloper
                SettingsViewModel.CATEGORY_ABOUT -> com.ndi.feature.ndibrowser.presentation.R.id.settingsCategoryAbout
                else -> com.ndi.feature.ndibrowser.presentation.R.id.settingsCategoryGeneral
            }
            binding.root.setOnClickListener {
                onCategorySelected(category.id)
            }
        }
    }
}
