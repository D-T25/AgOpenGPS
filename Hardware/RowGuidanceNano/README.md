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

This is a read-only prototype. It does not open a COM port in the AOG UI yet, change GPS guidance, engage row assist, or command steering. The next software step is a diagnostics view that selects the Nano's COM port and displays live ADC/voltage values. Steering modes (GPS only, GPS plus feeler assist, and feeler only) need separate implementation and field testing after the sensor input is verified.
