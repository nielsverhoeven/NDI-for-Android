package com.ndi.feature.ndibrowser.data.validation

class MultiAddressFormatter {

    fun formatMultipleAddresses(addresses: List<String>, maxDisplay: Int = 5): String {
        val trimmedUnique = addresses
            .asSequence()
            .map { it.trim() }
            .filter { it.isNotBlank() }
            .distinct()
            .toList()

        if (trimmedUnique.isEmpty()) {
            return "not configured"
        }

        val visible = trimmedUnique.take(maxDisplay)
        return if (trimmedUnique.size > maxDisplay) {
            visible.joinToString(", ") + ", ..."
        } else {
            visible.joinToString(", ")
        }
    }
}
