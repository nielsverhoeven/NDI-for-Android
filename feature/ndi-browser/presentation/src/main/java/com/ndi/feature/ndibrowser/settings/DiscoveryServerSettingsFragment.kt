package com.ndi.feature.ndibrowser.settings

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.appcompat.app.AlertDialog
import androidx.core.view.isVisible
import androidx.core.widget.addTextChangedListener
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.findNavController
import androidx.recyclerview.widget.ItemTouchHelper
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import com.ndi.core.model.DiscoveryServerDraftMode
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentDiscoveryServerSettingsBinding
import com.ndi.feature.ndibrowser.presentation.databinding.ItemDiscoveryServerBinding
import kotlinx.coroutines.launch

class DiscoveryServerSettingsFragment : Fragment() {

    private var binding: FragmentDiscoveryServerSettingsBinding? = null
    private val viewModel: DiscoveryServerSettingsViewModel by viewModels {
        DiscoveryServerSettingsViewModel.Factory(
            SettingsDependencies.requireDiscoveryServerRepository(),
        )
    }
    private lateinit var serverAdapter: DiscoveryServerAdapter

    // Track text changes without re-triggering after programmatic updates
    private var programmaticUpdate = false

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val b = FragmentDiscoveryServerSettingsBinding.inflate(inflater, container, false)
        binding = b
        return b.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val b = binding ?: return

        // Toolbar back navigation (hidden when embedded as a child fragment in the two-column layout)
        b.discoveryServersTopAppBar.isVisible = parentFragment == null
        b.discoveryServersTopAppBar.setNavigationOnClickListener {
            findNavController().popBackStack()
        }

        // Set up RecyclerView
        serverAdapter = DiscoveryServerAdapter(
            onToggle = { id, enabled -> viewModel.onToggleServerClicked(id, enabled) },
            onEdit = { id -> viewModel.onEditServerClicked(id) },
            onDelete = { id -> showDeleteConfirmationDialog(id) },
            onReorder = { idsInOrder -> viewModel.onServersReordered(idsInOrder) },
        )
        b.discoveryServerList.layoutManager = LinearLayoutManager(requireContext())
        b.discoveryServerList.adapter = serverAdapter

        // Drag-to-reorder via ItemTouchHelper
        val touchHelper = ItemTouchHelper(object : ItemTouchHelper.SimpleCallback(
            ItemTouchHelper.UP or ItemTouchHelper.DOWN, 0
        ) {
            override fun onMove(
                recyclerView: RecyclerView,
                viewHolder: RecyclerView.ViewHolder,
                target: RecyclerView.ViewHolder,
            ): Boolean {
                val from = viewHolder.adapterPosition
                val to = target.adapterPosition
                serverAdapter.moveItem(from, to)
                return true
            }

            override fun onSwiped(viewHolder: RecyclerView.ViewHolder, direction: Int) = Unit

            override fun clearView(recyclerView: RecyclerView, viewHolder: RecyclerView.ViewHolder) {
                super.clearView(recyclerView, viewHolder)
                viewModel.onServersReordered(serverAdapter.currentIds())
            }
        })
        touchHelper.attachToRecyclerView(b.discoveryServerList)

        // Wire add-form text inputs
        b.discoveryHostInput.addTextChangedListener {
            if (!programmaticUpdate) viewModel.onHostInputChanged(it?.toString().orEmpty())
        }
        b.discoveryPortInput.addTextChangedListener {
            if (!programmaticUpdate) viewModel.onPortInputChanged(it?.toString().orEmpty())
        }

        // Add button
        b.addDiscoveryServerButton.setOnClickListener {
            viewModel.onAddServerClicked()
        }

        // Save edit button
        b.saveEditButton.setOnClickListener {
            viewModel.onSaveEditClicked()
        }

        // Cancel edit button
        b.cancelEditButton.setOnClickListener {
            viewModel.onCancelEditClicked()
            clearFormFields()
        }

        // Collect UI state
        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                viewModel.uiState.collect { state -> render(state) }
            }
        }

        viewModel.onScreenVisible()
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }

    private fun render(state: DiscoveryServerSettingsUiState) {
        val b = binding ?: return

        // Update server list
        serverAdapter.submitList(state.servers)
        b.discoveryEmptyState.isVisible = state.servers.isEmpty()
        b.discoveryServerList.isVisible = state.servers.isNotEmpty()

        // Validation error
        b.discoveryValidationError.isVisible = state.validationError != null
        b.discoveryValidationError.text = state.validationError.orEmpty()

        // No-enabled-servers warning
        b.noEnabledServersWarning.isVisible = state.noEnabledServersWarning != null

        // Form mode: ADD vs EDIT
        val isEditMode = state.formMode == DiscoveryServerDraftMode.EDIT
        b.addDiscoveryServerButton.isVisible = !isEditMode
        b.saveEditButton.isVisible = isEditMode
        b.cancelEditButton.isVisible = isEditMode

        // Update fields only when ViewModel changes them programmatically (e.g. on edit clicked)
        programmaticUpdate = true
        if (b.discoveryHostInput.text?.toString() != state.hostInput) {
            b.discoveryHostInput.setText(state.hostInput)
        }
        if (b.discoveryPortInput.text?.toString() != state.portInput) {
            b.discoveryPortInput.setText(state.portInput)
        }
        programmaticUpdate = false
    }

    private fun clearFormFields() {
        val b = binding ?: return
        programmaticUpdate = true
        b.discoveryHostInput.setText("")
        b.discoveryPortInput.setText("")
        programmaticUpdate = false
    }

    private fun showDeleteConfirmationDialog(id: String) {
        MaterialAlertDialogBuilder(requireContext())
            .setTitle(R.string.discovery_delete_confirm_title)
            .setMessage(R.string.discovery_delete_confirm_message)
            .setPositiveButton(R.string.discovery_delete_confirm_positive) { _, _ ->
                viewModel.onDeleteServerClicked(id)
            }
            .setNegativeButton(R.string.discovery_delete_confirm_negative, null)
            .show()
    }
}

// ---------------------------------------------------------------------------
// RecyclerView Adapter
// ---------------------------------------------------------------------------

class DiscoveryServerAdapter(
    private val onToggle: (id: String, enabled: Boolean) -> Unit,
    private val onEdit: (id: String) -> Unit,
    private val onDelete: (id: String) -> Unit,
    private val onReorder: (idsInOrder: List<String>) -> Unit,
) : RecyclerView.Adapter<DiscoveryServerAdapter.ViewHolder>() {

    private val items = mutableListOf<DiscoveryServerEntry>()

    fun submitList(newList: List<DiscoveryServerEntry>) {
        items.clear()
        items.addAll(newList)
        notifyDataSetChanged()
    }

    fun moveItem(from: Int, to: Int) {
        val entry = items.removeAt(from)
        items.add(to, entry)
        notifyItemMoved(from, to)
    }

    fun currentIds(): List<String> = items.map { it.id }

    override fun getItemCount(): Int = items.size

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val binding = ItemDiscoveryServerBinding.inflate(
            LayoutInflater.from(parent.context), parent, false,
        )
        return ViewHolder(binding)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        holder.bind(items[position])
    }

    inner class ViewHolder(
        private val binding: ItemDiscoveryServerBinding,
    ) : RecyclerView.ViewHolder(binding.root) {

        fun bind(entry: DiscoveryServerEntry) {
            binding.serverDisplayLabel.text = entry.displayLabel
            binding.serverEnabledSwitch.isChecked = entry.enabled
            binding.serverEnabledSwitch.setOnCheckedChangeListener { _, isChecked ->
                onToggle(entry.id, isChecked)
            }
            binding.editServerButton.setOnClickListener { onEdit(entry.id) }
            binding.deleteServerButton.setOnClickListener { onDelete(entry.id) }
        }
    }
}
