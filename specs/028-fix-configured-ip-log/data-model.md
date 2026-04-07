# Data Model: Developer Log Configured IP Display

## Entity: DeveloperModeState
- Description: Boolean state controlling whether developer-only viewer logs are visible.
- Fields:
  - enabled: Boolean
- Validation rules:
  - When `enabled=false`, developer IP log output MUST NOT be shown.
- State transitions:
  - false -> true: developer logs become eligible for rendering.
  - true -> false: developer logs are hidden/suppressed.

## Entity: ConfiguredAddressSet
- Description: Active configured discovery/server address values consumed by viewer logging.
- Fields:
  - addresses: Ordered list of strings
- Validation rules:
  - Valid address types: IPv4 literal, IPv6 literal, hostname.
  - Malformed entries are excluded from rendered output.
  - Duplicate values are de-duplicated per emitted log event.
- Relationships:
  - Referenced by `DeveloperLogEntry` during emission.

## Entity: DeveloperLogEntry
- Description: View screen log record emitted in developer mode with optional configured-address content.
- Fields:
  - timestamp: Instant
  - eventType: String
  - message: String
  - configuredAddresses: Ordered list of valid addresses (optional)
  - fallbackReason: String (optional, used when no valid addresses)
- Validation rules:
  - If developer mode is disabled, entry with configured-address context is suppressed.
  - If no valid configured addresses exist, entry uses explicit "not configured"-style fallback text.
- Relationships:
  - Depends on `DeveloperModeState.enabled` and `ConfiguredAddressSet.addresses`.

## Derived Output Rules
- Rendered log output reflects active configuration at emission time.
- Multi-address output preserves active configuration order after de-duplication.
