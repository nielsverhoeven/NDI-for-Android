package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.data.validation.AddressValidator
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AddressValidatorTest {

    private val validator = AddressValidator()

    @Test
    fun validates_ipv4_ipv6_and_hostname_addresses() {
        assertTrue(validator.isValidAddress("192.168.1.1"))
        assertTrue(validator.isValidAddress("255.255.255.255"))
        assertTrue(validator.isValidAddress("ff02::1"))
        assertTrue(validator.isValidAddress("::1"))
        assertTrue(validator.isValidAddress("ndi-host.local"))
    }

    @Test
    fun rejects_malformed_addresses() {
        assertFalse(validator.isValidAddress("999.999.999.999"))
        assertFalse(validator.isValidAddress("bad host name"))
        assertFalse(validator.isValidAddress(":::::"))
    }

    @Test
    fun validateAndFilterAddresses_deduplicates_preserving_order() {
        val output = validator.validateAndFilterAddresses(
            listOf("192.168.1.10", "ff02::1", "192.168.1.10", "ndi-host.local"),
        )

        assertEquals(listOf("192.168.1.10", "ff02::1", "ndi-host.local"), output)
    }
}
