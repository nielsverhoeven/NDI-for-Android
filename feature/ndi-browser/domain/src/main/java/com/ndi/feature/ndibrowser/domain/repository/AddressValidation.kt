package com.ndi.feature.ndibrowser.domain.repository

interface AddressValidation {
    fun isValidAddress(value: String): Boolean

    fun validateAndFilterAddresses(addresses: List<String>): List<String>

    fun getDisplayText(addresses: List<String>): String
}
