package com.ndi.feature.ndibrowser.data.validation

import com.ndi.feature.ndibrowser.domain.repository.AddressValidation

class AddressValidator(
    private val formatter: MultiAddressFormatter = MultiAddressFormatter(),
) : AddressValidation {

    private val ipv4Pattern = Regex("""^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$""")
    private val hostnamePattern = Regex("""^(?=.{1,253}$)(?!-)([A-Za-z0-9-]{1,63}\.)*[A-Za-z0-9-]{1,63}$""")
    private val ipv6Pattern = Regex(
        """^\[?(?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4}){7}|(?:[0-9A-Fa-f]{1,4}:){1,7}:|(?:[0-9A-Fa-f]{1,4}:){1,6}:[0-9A-Fa-f]{1,4}|(?:[0-9A-Fa-f]{1,4}:){1,5}(?::[0-9A-Fa-f]{1,4}){1,2}|(?:[0-9A-Fa-f]{1,4}:){1,4}(?::[0-9A-Fa-f]{1,4}){1,3}|(?:[0-9A-Fa-f]{1,4}:){1,3}(?::[0-9A-Fa-f]{1,4}){1,4}|(?:[0-9A-Fa-f]{1,4}:){1,2}(?::[0-9A-Fa-f]{1,4}){1,5}|[0-9A-Fa-f]{1,4}:(?:(?::[0-9A-Fa-f]{1,4}){1,6})|:(?:(?::[0-9A-Fa-f]{1,4}){1,7}|:))\]?$""",
    )

    override fun isValidAddress(value: String): Boolean {
        val normalized = value.trim()
        if (normalized.isBlank()) return false

        if (ipv4Pattern.matches(normalized)) return true
        if (ipv6Pattern.matches(normalized)) return true
        if (hostnamePattern.matches(normalized)) {
            val labels = normalized.split('.')
            val isNumericDotted = labels.size == 4 && labels.all { it.all(Char::isDigit) }
            if (!isNumericDotted) return true
        }

        return false
    }

    override fun validateAndFilterAddresses(addresses: List<String>): List<String> {
        return addresses
            .asSequence()
            .map { it.trim() }
            .filter { it.isNotBlank() }
            .filter { isValidAddress(it) }
            .distinct()
            .toList()
    }

    override fun getDisplayText(addresses: List<String>): String {
        val valid = validateAndFilterAddresses(addresses)
        return formatter.formatMultipleAddresses(valid)
    }
}
