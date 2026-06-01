package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.data.validation.MultiAddressFormatter
import org.junit.Assert.assertEquals
import org.junit.Test

class MultiAddressFormatterTest {

    private val formatter = MultiAddressFormatter()

    @Test
    fun keeps_address_order_while_deduplicating() {
        val formatted = formatter.formatMultipleAddresses(
            listOf("192.168.1.10", "ff02::1", "192.168.1.10", "ndi-host.local"),
        )

        assertEquals("192.168.1.10, ff02::1, ndi-host.local", formatted)
    }

    @Test
    fun truncates_output_to_first_five_addresses_with_ellipsis() {
        val formatted = formatter.formatMultipleAddresses(
            listOf("a", "b", "c", "d", "e", "f", "g"),
            maxDisplay = 5,
        )

        assertEquals("a, b, c, d, e, ...", formatted)
    }
}
