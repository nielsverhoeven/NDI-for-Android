package com.ndi.feature.ndibrowser.viewer

import android.content.Context
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.presentation.R

data class QualityMenuItem(
    val profile: QualityProfile,
    val title: String,
    val hint: String,
    val contentDescription: String,
)

object QualitySettingsMenuComposable {
    fun buildItems(context: Context): List<QualityMenuItem> {
        return listOf(
            QualityMenuItem(
                profile = QualityProfile.SMOOTH,
                title = context.getString(R.string.ndi_viewer_quality_smooth),
                hint = context.getString(R.string.ndi_viewer_quality_hint_smooth),
                contentDescription = context.getString(R.string.ndi_viewer_quality_description_smooth),
            ),
            QualityMenuItem(
                profile = QualityProfile.BALANCED,
                title = context.getString(R.string.ndi_viewer_quality_balanced),
                hint = context.getString(R.string.ndi_viewer_quality_hint_balanced),
                contentDescription = context.getString(R.string.ndi_viewer_quality_description_balanced),
            ),
            QualityMenuItem(
                profile = QualityProfile.HIGH_QUALITY,
                title = context.getString(R.string.ndi_viewer_quality_high_quality),
                hint = context.getString(R.string.ndi_viewer_quality_hint_high_quality),
                contentDescription = context.getString(R.string.ndi_viewer_quality_description_high_quality),
            ),
        )
    }
}
