package com.ndi.core.model

enum class DiscoveryCheckType { ADD_VALIDATION, MANUAL_RECHECK }
enum class DiscoveryCheckOutcome { SUCCESS, FAILURE }
enum class DiscoveryFailureCategory { NONE, ENDPOINT_UNREACHABLE, HANDSHAKE_FAILED, TIMEOUT, UNKNOWN }
