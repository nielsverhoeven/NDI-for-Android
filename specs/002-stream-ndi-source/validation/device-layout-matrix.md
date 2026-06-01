# Device Layout Matrix (FR-013)

## Target Form Factors

| Device Class | Min Width Bucket | Orientation | Required Flow |
|---|---|---|---|
| Phone | < 600dp | Portrait and landscape | source select -> start output -> stop output |
| Tablet | >= 600dp | Portrait and landscape | source select -> start output -> stop output |

## Pass Thresholds

- Primary action controls are visible without overlap or clipping.
- Output state text is fully readable and not truncated in critical states.
- Start/stop/retry controls remain tappable in all targeted orientations.
- At least 95% of matrix runs pass for release readiness.

## Execution Record

| Date | Device/AVD | Orientation Set | Result | Notes |
|---|---|---|---|---|
| TBD | TBD | TBD | NOT_RUN | Pending first run |
