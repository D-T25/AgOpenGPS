# Corn row-feeler USB prototype

This branch adds the first read-only pieces for a corn-head row sensor:

- `Hardware/RowGuidanceNano/RowGuidanceNano.ino` reads an analog sensor on Nano A0 at 50 Hz and sends a framed packet over USB serial.
- `SourceCode/GPS/Classes/RowGuidance/RowSensorPacket.cs` parses that packet for use by AgOpenGPS.

## USB packet

`ROW1,<sequence>,<ADC counts>,<millivolts>\n`

The Nano uses 115200 baud. On a classic Nano, ADC counts range from 0 to 1023. The firmware reports raw readings without smoothing so sensor behavior can be inspected before tuning.

## Wiring notes

- Sensor output signal to A0.
- Sensor signal ground to Nano ground.
- Power the crop sensor from its specified supply. Do not assume the Nano can power it.
- The expected Headsight-style crop signal is roughly 1–4 V, with about 2.5 V centered. Confirm the actual sensor and harness pinout before connecting it.

## Current scope

The branch includes a harvester-only Row Guidance Setup screen in Auto Steer Configuration. The setup screen stores the selected mode, COM port, center voltage, direction, deadband, correction gain/limit, engage delay, and filter factor in the current vehicle profile. A single AOG-owned USB reader supplies both the diagnostics view and the row-guidance calculation.

The steering calculation now produces a bounded row-corrected candidate target for GPS-plus-feeler and feeler-only modes. GPS-plus-feeler falls back to the GPS target when packets are stale; feeler-only refuses the candidate without fresh data. Row assist is bypassed and disarmed on U-turns and when AutoSteer is disengaged. The steering-output hard-off in `RowGuidanceRuntime.SteeringOutputEnabled` remains `false`, so the candidate is not sent to the steer PGN. Build and verify serial parsing, signal-loss handling, mode transitions, correction direction, and bounded steering behavior before changing that flag.
