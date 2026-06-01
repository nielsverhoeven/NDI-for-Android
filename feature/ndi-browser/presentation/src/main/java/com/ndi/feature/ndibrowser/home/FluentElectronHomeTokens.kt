package com.ndi.feature.ndibrowser.home

import android.content.Context
import android.util.TypedValue
import android.widget.TextView
import com.google.android.material.card.MaterialCardView
import com.ndi.feature.ndibrowser.presentation.R

/** Shared presentation token mapper for the home dashboard cards in feature 032. */
object FluentElectronHomeTokens {

    fun applyHomeCards(
        context: Context,
        streamCard: MaterialCardView,
        viewCard: MaterialCardView,
        subtitle: TextView,
    ) {
        val stroke = context.resources.getColor(R.color.fluent_electron_stroke_subtle, context.theme)
        val surface = context.resources.getColor(R.color.fluent_electron_surface_card, context.theme)
        val secondaryText = context.resources.getColor(R.color.fluent_electron_text_secondary, context.theme)

        listOf(streamCard, viewCard).forEach { card ->
            card.radius = dp(context, 12f)
            card.strokeWidth = dp(context, 1f).toInt()
            card.strokeColor = stroke
            card.setCardBackgroundColor(surface)
        }

        subtitle.setTextColor(secondaryText)
    }

    private fun dp(context: Context, value: Float): Float =
        TypedValue.applyDimension(
            TypedValue.COMPLEX_UNIT_DIP,
            value,
            context.resources.displayMetrics,
        )
}
