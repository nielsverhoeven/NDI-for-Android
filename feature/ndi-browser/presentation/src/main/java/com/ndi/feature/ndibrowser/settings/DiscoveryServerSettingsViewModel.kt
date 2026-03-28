package com.ndi.feature.ndibrowser.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.DiscoveryServerDraftMode
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.launch

data class DiscoveryServerSettingsUiState(
    val servers: List<DiscoveryServerEntry> = emptyList(),
    val hostInput: String = "",
    val portInput: String = "",
    val validationError: String? = null,
    val isSaveEnabled: Boolean = false,
    val isBusy: Boolean = false,
    val formMode: DiscoveryServerDraftMode = DiscoveryServerDraftMode.ADD,
    val editingEntryId: String? = null,
    val noEnabledServersWarning: String? = null,
)

class DiscoveryServerSettingsViewModel(
    private val repository: DiscoveryServerRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DiscoveryServerSettingsUiState())
    val uiState: StateFlow<DiscoveryServerSettingsUiState> = _uiState.asStateFlow()

    init {
        repository.observeServers()
            .onEach { servers ->
                _uiState.value = _uiState.value.copy(
                    servers = servers,
                    noEnabledServersWarning = noEnabledWarning(servers),
                )
            }
            .launchIn(viewModelScope)
    }

    fun onScreenVisible() = Unit

    fun onHostInputChanged(input: String) {
        val error = validateHost(input)
        val portError = validatePort(_uiState.value.portInput)
        val combined = error ?: portError
        _uiState.value = _uiState.value.copy(
            hostInput = input,
            validationError = combined,
            isSaveEnabled = combined == null && input.isNotBlank(),
        )
    }

    fun onPortInputChanged(input: String) {
        val error = validatePort(input)
        val hostError = validateHost(_uiState.value.hostInput)
        val combined = error ?: hostError
        _uiState.value = _uiState.value.copy(
            portInput = input,
            validationError = combined,
            isSaveEnabled = combined == null && _uiState.value.hostInput.isNotBlank(),
        )
    }

    fun onAddServerClicked() {
        val state = _uiState.value
        val hostError = validateHost(state.hostInput)
        val portError = validatePort(state.portInput)
        val error = hostError ?: portError
        if (error != null) {
            _uiState.value = state.copy(validationError = error, isSaveEnabled = false)
            return
        }

        _uiState.value = state.copy(isBusy = true, validationError = null)
        viewModelScope.launch {
            runCatching {
                repository.addServer(state.hostInput, state.portInput)
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    hostInput = "",
                    portInput = "",
                    isBusy = false,
                    isSaveEnabled = false,
                    validationError = null,
                )
            }.onFailure { e ->
                _uiState.value = _uiState.value.copy(
                    isBusy = false,
                    validationError = e.message,
                    isSaveEnabled = false,
                )
            }
        }
    }

    fun onEditServerClicked(id: String) {
        val server = _uiState.value.servers.firstOrNull { it.id == id } ?: return
        _uiState.value = _uiState.value.copy(
            hostInput = server.hostOrIp,
            portInput = server.port.toString(),
            formMode = DiscoveryServerDraftMode.EDIT,
            editingEntryId = id,
            validationError = null,
            isSaveEnabled = true,
        )
    }

    fun onSaveEditClicked() {
        val state = _uiState.value
        val id = state.editingEntryId ?: return
        val hostError = validateHost(state.hostInput)
        val portError = validatePort(state.portInput)
        val error = hostError ?: portError
        if (error != null) {
            _uiState.value = state.copy(validationError = error, isSaveEnabled = false)
            return
        }

        _uiState.value = state.copy(isBusy = true, validationError = null)
        viewModelScope.launch {
            runCatching {
                repository.updateServer(id, state.hostInput, state.portInput)
            }.onSuccess {
                _uiState.value = _uiState.value.copy(
                    hostInput = "",
                    portInput = "",
                    isBusy = false,
                    isSaveEnabled = false,
                    formMode = DiscoveryServerDraftMode.ADD,
                    editingEntryId = null,
                    validationError = null,
                )
            }.onFailure { e ->
                _uiState.value = _uiState.value.copy(
                    isBusy = false,
                    validationError = e.message,
                    isSaveEnabled = false,
                )
            }
        }
    }

    fun onCancelEditClicked() {
        _uiState.value = _uiState.value.copy(
            hostInput = "",
            portInput = "",
            formMode = DiscoveryServerDraftMode.ADD,
            editingEntryId = null,
            validationError = null,
            isSaveEnabled = false,
        )
    }

    fun onDeleteServerClicked(id: String) {
        viewModelScope.launch {
            runCatching { repository.removeServer(id) }
        }
    }

    fun onToggleServerClicked(id: String, enabled: Boolean) {
        viewModelScope.launch {
            runCatching { repository.setServerEnabled(id, enabled) }
        }
    }

    fun onServersReordered(idsInOrder: List<String>) {
        viewModelScope.launch {
            runCatching { repository.reorderServers(idsInOrder) }
        }
    }

    fun onDismissValidationError() {
        _uiState.value = _uiState.value.copy(validationError = null)
    }

    private fun validateHost(input: String): String? {
        if (input.isBlank()) return "Hostname or IP address is required."
        if (input.trim().length > 253) return "Hostname is too long."
        return null
    }

    private fun validatePort(input: String): String? {
        if (input.isBlank()) return null
        val port = input.trim().toIntOrNull() ?: return "Port must be a valid number."
        if (port !in 1..65535) return "Port must be between 1 and 65535."
        return null
    }

    private fun noEnabledWarning(servers: List<DiscoveryServerEntry>): String? {
        if (servers.isEmpty()) return null
        return if (servers.none { it.enabled }) {
            "No discovery servers are enabled. Enable at least one for discovery to work."
        } else null
    }

    class Factory(
        private val repository: DiscoveryServerRepository,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return DiscoveryServerSettingsViewModel(repository) as T
        }
    }
}