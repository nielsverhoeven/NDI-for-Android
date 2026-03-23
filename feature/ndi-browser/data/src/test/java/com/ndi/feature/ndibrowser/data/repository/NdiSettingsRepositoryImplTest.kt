package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDiscoveryEndpoint
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NdiSettingsRepositoryImplTest {

    @Test
    fun parse_hostname_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("ndi-server.local")
        assertNotNull(parsed)
        assertEquals("ndi-server.local", parsed?.host)
        assertNull(parsed?.port)
        assertTrue(parsed?.usesDefaultPort == true)
    }

    @Test
    fun parse_ipv4_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("192.168.1.10")
        assertNotNull(parsed)
        assertEquals("192.168.1.10", parsed?.host)
        assertNull(parsed?.port)
    }

    @Test
    fun parse_ipv4_withPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("192.168.1.10:7000")
        assertNotNull(parsed)
        assertEquals("192.168.1.10", parsed?.host)
        assertEquals(7000, parsed?.port)
    }

    @Test
    fun parse_ipv6Bracketed_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("[::1]")
        assertNotNull(parsed)
        assertEquals("::1", parsed?.host)
        assertNull(parsed?.port)
        assertTrue(parsed?.usesDefaultPort == true)
    }

    @Test
    fun parse_ipv6Bracketed_withPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("[::1]:5960")
        assertNotNull(parsed)
        assertEquals("::1", parsed?.host)
        assertEquals(5960, parsed?.port)
    }

    @Test
    fun parse_trimsWhitespace() {
        val parsed = NdiDiscoveryEndpoint.parse("  ndi-server.local  ")
        assertNotNull(parsed)
        assertEquals("ndi-server.local", parsed?.host)
    }

    @Test
    fun parse_emptyString_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse(""))
    }

    @Test
    fun parse_blankString_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse("  "))
    }

    @Test
    fun parse_nullInput_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse(null))
    }

    @Test
    fun parse_unbracketedIpv6WithPort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("::1:5960"))
    }

    @Test
    fun parse_invalidNegativePort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:-1"))
    }

    @Test
    fun parse_outOfRangePort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:65536"))
    }

    @Test
    fun parse_zeroPort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:0"))
    }

    @Test
    fun parse_maxPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("host:65535")
        assertNotNull(parsed)
        assertEquals(65535, parsed?.port)
    }

    @Test
    fun parse_minPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("host:1")
        assertNotNull(parsed)
        assertEquals(1, parsed?.port)
    }

    @Test
    fun isValidPort_acceptsLowerBound() {
        assertTrue(NdiDiscoveryEndpoint.isValidPort(1))
    }

    @Test
    fun isValidPort_acceptsUpperBound() {
        assertTrue(NdiDiscoveryEndpoint.isValidPort(65535))
    }

    @Test
    fun isValidPort_rejectsZero() {
        assertFalse(NdiDiscoveryEndpoint.isValidPort(0))
    }

    @Test
    fun isValidPort_rejectsAboveUpperBound() {
        assertFalse(NdiDiscoveryEndpoint.isValidPort(65536))
    }
}