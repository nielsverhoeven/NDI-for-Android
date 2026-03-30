package com.ndi.feature.ndibrowser.source_list.adapter

import android.graphics.BitmapFactory
import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.core.view.isVisible
import androidx.recyclerview.widget.RecyclerView
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.ItemNdiSourceBinding

class SourceAdapter(
    private val onViewStreamClicked: (String) -> Unit,
) : RecyclerView.Adapter<SourceViewHolder>() {

    companion object {
        const val SOURCE_ROW_CONTAINER_TEST_TAG = "source-list-row-container"
        const val VIEW_STREAM_BUTTON_TEST_TAG = "source-list-view-stream-button"
    }

    private var items: List<NdiSource> = emptyList()
    private var highlightedSourceId: String? = null

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SourceViewHolder {
        val binding = ItemNdiSourceBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return SourceViewHolder(binding, onViewStreamClicked)
    }

    override fun onBindViewHolder(holder: SourceViewHolder, position: Int) {
        holder.bind(items[position], items[position].sourceId == highlightedSourceId)
    }

    override fun getItemCount(): Int = items.size

    fun submitList(newItems: List<NdiSource>, highlightedSourceId: String?) {
        items = newItems
        this.highlightedSourceId = highlightedSourceId
        notifyDataSetChanged()
    }
}

class SourceViewHolder(
    private val binding: ItemNdiSourceBinding,
    private val onViewStreamClicked: (String) -> Unit,
) : RecyclerView.ViewHolder(binding.root) {

    fun bind(source: NdiSource, isHighlighted: Boolean) {
        binding.sourceName.text = source.displayName
        binding.sourceEndpoint.text = if (source.sourceId.startsWith("device-screen:")) {
            binding.root.context.getString(R.string.ndi_source_local_screen_endpoint)
        } else {
            source.endpointAddress ?: source.sourceId
        }
        val previewBitmap = source.lastFramePreviewPath?.let(BitmapFactory::decodeFile)
        binding.sourcePreviewImage.isVisible = previewBitmap != null
        binding.sourcePreviewImage.setImageBitmap(previewBitmap)
        binding.highlightBadge.isVisible = isHighlighted
        
        // T035: Render Previously Connected badge
        binding.previouslyConnectedBadge.isVisible = source.previouslyConnected
        
        // T035: Render Unavailable badge
        binding.unavailableBadge.isVisible = !source.isAvailable
        
        // T037: Update button enabled state based on availability
        binding.viewStreamButton.isEnabled = source.isAvailable
        
        binding.root.setOnClickListener(null)
        binding.root.isClickable = false
        binding.root.isFocusable = false
        binding.root.tag = SourceAdapter.SOURCE_ROW_CONTAINER_TEST_TAG
        binding.viewStreamButton.tag = SourceAdapter.VIEW_STREAM_BUTTON_TEST_TAG
        binding.viewStreamButton.setOnClickListener { onViewStreamClicked(source.sourceId) }
    }
}
