/*
 * AgOpenGPS corn row-feeler USB prototype
 *
 * Reads one sensor on A0, and automatically adds a second sensor on A1 when
 * that input has a plausible signal for several consecutive samples. In
 * one-channel installations, fit a 100 kOhm pull-down from A1 to GND so the
 * unused input cannot float into the detection range.
 *
 * The sketch reports measurements only; it does not steer or command a valve.
 */
#include <Arduino.h>

constexpr uint8_t LEFT_SENSOR_PIN = A0;
constexpr uint8_t RIGHT_SENSOR_PIN = A1;
constexpr uint32_t SERIAL_BAUD = 115200;
constexpr uint32_t SAMPLE_PERIOD_MS = 20; // 50 Hz
constexpr uint16_t SENSOR_PRESENT_MIN_MV = 700;
constexpr uint16_t SENSOR_PRESENT_MAX_MV = 4300;
constexpr uint8_t SENSOR_PRESENT_CONFIRMATIONS = 10;

uint32_t sequenceNumber = 0;
uint32_t lastSampleMs = 0;
uint8_t rightSensorPresentCount = 0;
bool dualSensorMode = false;

uint16_t ReadMillivolts(uint8_t pin, uint16_t &raw) {
  raw = analogRead(pin);
  return static_cast<uint16_t>((static_cast<uint32_t>(raw) * 5000UL + 511UL) / 1023UL);
}

void setup() {
  Serial.begin(SERIAL_BAUD);
  analogReference(DEFAULT); // Nano's 5 V supply is the ADC reference
}

void loop() {
  const uint32_t now = millis();
  if (static_cast<uint32_t>(now - lastSampleMs) < SAMPLE_PERIOD_MS) return;
  lastSampleMs = now;

  uint16_t leftAdc = 0;
  uint16_t rightAdc = 0;
  const uint16_t leftMillivolts = ReadMillivolts(LEFT_SENSOR_PIN, leftAdc);
  const uint16_t rightMillivolts = ReadMillivolts(RIGHT_SENSOR_PIN, rightAdc);

  if (!dualSensorMode && rightMillivolts >= SENSOR_PRESENT_MIN_MV &&
      rightMillivolts <= SENSOR_PRESENT_MAX_MV) {
    if (rightSensorPresentCount < SENSOR_PRESENT_CONFIRMATIONS) ++rightSensorPresentCount;
    if (rightSensorPresentCount >= SENSOR_PRESENT_CONFIRMATIONS) dualSensorMode = true;
  } else if (!dualSensorMode) {
    rightSensorPresentCount = 0;
  }

  if (dualSensorMode) {
    // Stay in dual mode after detection. If a sensor is unplugged, AOG sees
    // the invalid channel and falls back instead of silently changing modes.
    Serial.print(F("ROW2,"));
    Serial.print(sequenceNumber++);
    Serial.print(',');
    Serial.print(leftAdc);
    Serial.print(',');
    Serial.print(leftMillivolts);
    Serial.print(',');
    Serial.print(rightAdc);
    Serial.print(',');
    Serial.println(rightMillivolts);
  } else {
    Serial.print(F("ROW1,"));
    Serial.print(sequenceNumber++);
    Serial.print(',');
    Serial.print(leftAdc);
    Serial.print(',');
    Serial.println(leftMillivolts);
  }
}
