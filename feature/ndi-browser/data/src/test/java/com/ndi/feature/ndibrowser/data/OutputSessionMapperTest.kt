package com.ndi.feature.ndibrowser.data

import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class OutputSessionMapperTest {

    @Test
    fun createStartingSession_resolvesConflictWithNumericSuffix() {
        val mapper = OutputSessionMapper()

        val session = mapper.createStartingSession(
            inputSourceId = "camera-1",
            preferredName = "My Stream",
            activeStreamNames = setOf("My Stream", "My Stream (2)"),
        )

        assertEquals("My Stream (3)", session.outboundStreamName)
        assertEquals("camera-1", session.inputSourceId)
        assertTrue(session.sessionId.isNotBlank())
    }

    @Test
    fun createStartingSession_usesDefaultNameWhenBlank() {
        val mapper = OutputSessionMapper()

        val session = mapper.createStartingSession(
            inputSourceId = "camera-2",
            preferredName = "   ",
            activeStreamNames = emptySet(),
        )

        assertEquals("NDI Output", session.outboundStreamName)
    }
}
