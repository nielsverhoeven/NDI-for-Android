package com.ndi.feature.ndibrowser.settings

import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

@RunWith(JUnit4::class)
class OverlayLogRedactorTest {

    @Test
    fun redact_replacesIpv4() {
        assertEquals(
            "Connecting to [redacted-ip]",
            OverlayLogRedactor.redact("Connecting to 192.168.1.100"),
        )
    }

    @Test
    fun redact_replacesIpv4AtEndOfString() {
        assertEquals(
            "Resolved endpoint [redacted-ip]",
            OverlayLogRedactor.redact("Resolved endpoint 192.168.1.100"),
        )
    }

    @Test
    fun redact_replacesBracketedIpv6() {
        assertEquals(
            "Loopback [redacted-ip] ready",
            OverlayLogRedactor.redact("Loopback [::1] ready"),
        )
    }

    @Test
    fun redact_replacesUnbracketedIpv6() {
        assertEquals(
            "Peer [redacted-ip] connected",
            OverlayLogRedactor.redact("Peer 2001:db8::1 connected"),
        )
    }

    @Test
    fun redact_leavesStringsWithoutIpUntouched() {
        assertEquals(
            "No IP present",
            OverlayLogRedactor.redact("No IP present"),
        )
    }

    @Test
    fun redact_replacesMultipleIps() {
        assertEquals(
            "[redacted-ip] -> [redacted-ip]",
            OverlayLogRedactor.redact("192.168.1.10 -> [fe80::1]"),
        )
    }

    @Test
    fun redact_handlesEmptyString() {
        assertEquals("", OverlayLogRedactor.redact(""))
    }

    @Test
    fun redact_preservesPortAfterIpv4() {
        assertEquals(
            "Connect [redacted-ip]:5960",
            OverlayLogRedactor.redact("Connect 192.168.1.1:5960"),
        )
    }
}