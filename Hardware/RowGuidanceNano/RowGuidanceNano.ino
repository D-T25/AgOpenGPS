/*
 * AgOpenGPS corn row-feeler USB prototype
 *
 * Reads the analog crop-sensor output on A0 and streams ROW1 packets over USB.
 * This firmware only reports sensor data; it does not steer or command a valve.
 *
 * Expected sensor: Headsight TrueSight-style Hall-effect crop sensor, nominal
 * output 1-4 V and approximately 2.5 V when centered. Use the sensor's specified
 * supply and share signal ground with the Nano. Do not connect sensor power to
 * the Nano unless the sensor documentation explicitly allows it.
 */
#include <Arduino.h>

constexpr uint8_t SENSOR_PIN = A0;
constexpr uint32_t SERIAL_BAUD = 115200;
constexpr uint32_t SAMPLE_PERIOD_MS = 20; // 50 Hz

uint32_t sequenceNumber = 0;
uint32_t lastSampleMs = 0;

void setup() {
  Serial.begin(SERIAL_BAUD);
  analogReference(DEFAULT); // Nano's 5 V supply is the ADC reference
}

void loop() {
  const uint32_t now = millis();
  if (static_cast<uint32_t>(now - lastSampleMs) < SAMPLE_PERIOD_MS) {
    return;
  }
  lastSampleMs = now;

  const uint16_t raw = analogRead(SENSOR_PIN); // 0..1023 on classic Nano
  const uint16_t millivolts =
      static_cast<uint16_t>((static_cast<uint32_t>(raw) * 5000UL + 511UL) / 1023UL);

  // Framing: ROW1,<sequence>,<ADC counts>,<millivolts>\n
  Serial.print(F("ROW1,"));
  Serial.print(sequenceNumber++);
  Serial.print(',');
  Serial.print(raw);
  Serial.print(',');
  Serial.println(millivolts);
}
