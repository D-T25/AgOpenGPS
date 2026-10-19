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

The branch includes a read-only diagnostics view plus a harvester-only Row Guidance Setup screen in Auto Steer Configuration. The setup screen stores the selected mode, COM port, center voltage, direction, deadband, correction gain/limit, engage delay, and filter factor in the current vehicle profile.

The engage control currently checks the operator interface only. AOG does not yet read live packets into the steering loop, and no row-feeler mode changes the steering command. The steering-output feature flag is intentionally off. Build and verify serial parsing, signal-loss handling, mode transitions, and constrained steering behavior before enabling it.
