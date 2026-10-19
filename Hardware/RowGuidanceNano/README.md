# Corn row-feeler guidance prototype

Experimental AgOpenGPS integration for reading a corn-head row-feeler sensor through an Arduino Nano and USB serial. This branch keeps the setup, diagnostics, sensor reader, and candidate correction calculation inside AgOpenGPS.

> **Prototype status:** The correction is calculated for display and evaluation, but the steering-output hard-off remains enabled in `RowGuidanceRuntime.SteeringOutputEnabled`. The row-feeler candidate cannot reach the steering controller. Do not change that setting until the code has passed a Windows build, bench validation, and a controlled steering review.

## What is included

- Nano firmware that samples A0 and A1 at 50 Hz and automatically sends one- or two-channel packets over USB.
- AOG packet parsing for both channel counts and one shared serial connection for diagnostics and guidance calculations.
- A harvester-only setup form with GPS-only, GPS-plus-feeler, and feeler-only modes.
- Vehicle-profile settings for COM port, each channel's center voltage and direction, deadband, gain, maximum correction, engage delay, and filter strength.
- Candidate correction calculation with stale-data behavior, turn bypass, and correction/vehicle-angle limits.

## One-sensor and two-sensor support

The Nano and AOG now support both packet layouts. With one sensor, connect it to A0 and the Nano sends `ROW1`. With two separate sensors, connect left to A0 and right to A1; after ten consecutive plausible right-channel samples, the Nano switches to `ROW2`, and AOG detects the two-channel packet automatically. The switch to dual mode stays latched until the Nano restarts. If either channel then goes out of range, AOG marks the row data invalid rather than silently using only one sensor.

Fit a 100 kOhm pull-down resistor from each sensor input (A0 and A1) to Nano ground so an unplugged input reads low instead of floating. In a one-sensor installation, at minimum A1 needs this pull-down to prevent false dual-sensor detection. Confirm the pull-down will not affect the actual sensor output. Automatic detection uses a prototype 0.7–4.3 V presence window; verify that against the actual sensor before relying on detection. The AOG runtime currently accepts 0.5–4.5 V as a usable channel range.

Each channel has its own center-voltage and invert setting. AOG applies the shared gain, deadband, filter, and correction limit after normalizing both channels so positive values mean the same physical correction direction. In dual mode it requires both channels to be valid and rejects opposing non-neutral correction signs; otherwise it averages their normalized errors. That is a starting model, not a validated Deere-equivalent algorithm; sensor locations and signal polarity must be confirmed on the actual head.

## Data path

1. The sensor voltage is read on Nano A0.
2. The Nano sends `ROW1` (one sensor) or `ROW2` (two sensors) packets over USB at 115200 baud.
3. AgOpenGPS validates each packet and treats data older than 0.5 seconds as stale.
4. The selected mode and calibration settings produce a candidate steering target.
5. The hard-off gate prevents that candidate from being sent to the steering controller.

| Mode | Candidate behavior |
|---|---|
| GPS only | Uses the normal AgOpenGPS GPS guidance target. |
| GPS + feeler assist | Adds a calibrated, bounded row correction to the GPS target; falls back to GPS when row data is stale. |
| Feeler only | Uses the calibrated sensor correction; does not provide a candidate when row data is stale or the engage delay has not elapsed. |

Row assist is bypassed during U-turns. Disengaging AutoSteer or row assist clears the row-assist state. These rules do not replace the machine’s normal safety controls.

## Hardware and wiring

- Arduino Nano with USB serial connection to the Windows computer running AgOpenGPS.
- Crop sensor output signal to Nano A0.
- Sensor signal ground connected to Nano ground.
- Power the sensor from its specified supply. Do not assume the Nano can power it.
- Keep A0 within the Nano’s permitted input voltage. Confirm the sensor’s supply, signal range, and connector pinout from its documentation before wiring.

The initial sensor estimate was roughly 1–4 V with about 2.5 V centered. Treat those as values to verify for the actual sensor, not as guaranteed specifications.

## Nano serial packet

```text
ROW1,<sequence>,<ADC counts>,<millivolts>\n
ROW2,<sequence>,<left ADC>,<left millivolts>,<right ADC>,<right millivolts>\n
```

The sketch uses 115200 baud, 8 data bits, no parity, and 1 stop bit. For a classic 5 V Nano, ADC readings are 0–1023. The sketch reports unsmoothed readings; filtering is configured in AgOpenGPS.

## Configure AgOpenGPS

1. Build and run the `corn-row-guidance` branch on Windows.
2. Open **Auto Steer Configuration** and choose **Row Guidance Setup** on the harvester profile.
3. Connect the Nano, refresh the COM-port list, and select its port.
4. Start with **GPS only** and open row-sensor diagnostics to confirm live readings.
5. With the sensor centered, record its stable voltage and set the center value. Tune invert direction, deadband, gain, correction limit, filter, and engage delay only while steering output remains disabled.
6. Treat the displayed candidate as diagnostic information. It is not an approved steering command.

## Bench-test checklist

### Before powering the sensor

- [ ] Park and secure the machine. Keep the hydraulic steering output physically disabled or disconnected during bench checks.
- [ ] Leave AOG AutoSteer off and confirm `SteeringOutputEnabled` is `false`.
- [ ] Check the sensor documentation for supply voltage, pinout, signal range, and ground reference. Keep Nano A0 within its permitted voltage.
- [ ] Build the solution on Windows and fix compile errors before testing the hardware.

### Verify the Nano and serial link

- [ ] Upload `RowGuidanceNano.ino` to the Nano.
- [ ] Open a serial monitor at 115200 baud and confirm `ROW1` lines arrive about 50 times per second.
- [ ] For one-sensor wiring, confirm A1 is held near ground with the 100 kOhm pull-down and the Nano stays in `ROW1` mode.
- [ ] Add a second sensor to A1 and confirm the Nano changes to `ROW2` after its detection delay. Confirm it stays in `ROW2` if a channel is later unplugged.
- [ ] Move the sensor through its range and confirm ADC counts and millivolts change smoothly without exceeding expected limits.
- [ ] Close the serial monitor before connecting from AgOpenGPS, since only one program can own the COM port at a time.

### Verify AgOpenGPS behavior

- [ ] Connect from AOG diagnostics and confirm the selected port opens, packets update, and packet age remains below 0.5 seconds.
- [ ] Unplug USB or stop the data stream. Confirm AOG reports stale/missing data; GPS-plus-feeler returns to the GPS candidate, while feeler-only does not offer a valid candidate.
- [ ] Confirm GPS-only leaves the normal guidance target unchanged.
- [ ] With steering still physically disabled, check center calibration, invert direction, deadband, filter, and engage delay.
- [ ] Increase the test signal offset and confirm the candidate follows the selected direction and stays within the correction cap and vehicle-angle limit.
- [ ] Disengage row assist and AutoSteer; confirm the row-assist state clears. Confirm row assist is bypassed during a U-turn.
- [ ] Reconnect the sensor after signal loss and confirm valid data returns before any candidate is considered ready.
- [ ] In dual-channel mode, calibrate each channel separately; disconnect either channel and confirm AOG rejects the pair instead of silently reverting to one-channel correction.
- [ ] With steering still disabled, move the sensors so their calibrated correction signs oppose; confirm AOG reports disagreement and rejects row guidance until the calibration or contact is corrected.

## Before any steering test

The Windows build, serial checks, fallback checks, and correction-direction review must pass first. Have a qualified operator review the integration and machine controls before any low-speed test. Keep the hard-off in place until that review explicitly approves a separately validated change. This prototype does not claim valve-safe behavior.

## Source files

- Firmware: `Hardware/RowGuidanceNano/RowGuidanceNano.ino`
- Packet parser: `SourceCode/GPS/Classes/RowGuidance/RowSensorPacket.cs`
- Shared serial reader and correction calculation: `SourceCode/GPS/Classes/RowGuidance/RowGuidanceRuntime.cs`
- Setup and diagnostics forms: `SourceCode/GPS/Forms/FormRowGuidanceSettings.cs` and `FormRowSensorDiagnostics.cs`
- Guidance integration and hard-off gate: `SourceCode/GPS/Forms/Position.designer.cs`
