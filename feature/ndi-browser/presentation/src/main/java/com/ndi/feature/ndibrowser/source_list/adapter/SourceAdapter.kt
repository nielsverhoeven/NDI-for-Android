package com.ndi.feature.ndibrowser.source_list.adapter

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.core.view.isVisible
import androidx.recyclerview.widget.RecyclerView
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.presentation.databinding.ItemNdiSourceBinding

class SourceAdapter(
    private val onSourceClicked: (String) -> Unit,
    private val onOutputClicked: (String) -> Unit,
) : RecyclerView.Adapter<SourceViewHolder>() {

    private var items: List<NdiSource> = emptyList()
    private var highlightedSourceId: String? = null

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SourceViewHolder {
        val binding = ItemNdiSourceBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return SourceViewHolder(binding, onSourceClicked, onOutputClicked)
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
    private val onSourceClicked: (String) -> Unit,
    private val onOutputClicked: (String) -> Unit,
) : RecyclerView.ViewHolder(binding.root) {

    fun bind(source: NdiSource, isHighlighted: Boolean) {
        binding.sourceName.text = source.displayName
        binding.sourceEndpoint.text = source.endpointAddress ?: source.sourceId
        binding.highlightBadge.isVisible = isHighlighted
        binding.root.setOnClickListener { onSourceClicked(source.sourceId) }
        binding.outputButton.setOnClickListener { onOutputClicked(source.sourceId) }
    }
}
