package com.ndi.feature.ndibrowser.data.util

import org.junit.Assert.*
import org.junit.Test

class LogRedactorTest {

    @Test
    fun `redact removes IPv4 discovery server addresses from log string`() {
        val raw = "discovery_server_check_started host=192.168.1.100 port=5959 correlationId=abc"
        val redacted = LogRedactor.redact(raw)
        assertFalse("IPv4 address must not appear in redacted output", redacted.contains("192.168.1.100"))
    }

    @Test
    fun `redact removes hostname-like server addresses from log string`() {
        val raw = "discovery_server_check_started host=ndi-server.local port=5959 correlationId=abc"
        val redacted = LogRedactor.redact(raw)
        assertFalse("Hostname must not appear in redacted output", redacted.contains("ndi-server.local"))
    }

    @Test
    fun `redact removes port numbers from log string`() {
        val raw = "check host=ndi-server.local port=5959 done"
        val redacted = LogRedactor.redact(raw)
        assertFalse("Port must not appear in redacted output", redacted.contains("5959"))
    }

    @Test
    fun `redact returns non-empty string for non-empty input`() {
        val raw = "discovery_server_check_started host=192.168.1.1 port=5959"
        val redacted = LogRedactor.redact(raw)
        assertTrue(redacted.isNotBlank())
    }

    @Test
    fun `redact returns empty string for empty input`() {
        assertEquals("", LogRedactor.redact(""))
    }
}
