// ESP32-C3 (DFRobot Beetle) + TCA9548A + 2x DRV2605 + 2x LRA, driven over Wi-Fi/UDP from Unity.
//
// Differences from tools/beetle_wifi_unity.ino (which is left unchanged):
//   * Unity can send either 1 byte (level, as before) or 3 bytes (level, beat depth, beat rate, see BeatPacket.cs /
//     docs/esp-wifi.md). In the 3-byte case THIS firmware makes the beating itself and updates the actuators at
//     500 Hz, so beat rates far above what Unity's frame rate allows (up to about 60 Hz) are possible.
//   * Safety timeout: no valid packet for TIMEOUT_MS -> vibration off.
//   * Wi-Fi name and password live in secrets.h (git-ignored): copy secrets.example.h to secrets.h and edit it.
//   * Same hardware setup (LRA open loop at 170 Hz, both actuators get the same value).
//
// Packet: byte 0 = level 0..127; byte 1 = beat depth 0..255 (= 0..1); byte 2 = beat rate in 0.5 Hz steps.
// A 1-byte packet means "no beating".
//
// NOTE: an LRA is a resonant device. How fast an amplitude wobble it can still reproduce depends on the actuator
// (its mechanical bandwidth around 170 Hz); above some rate the beating will be felt weaker or not at all. Find out
// by trying rates on the bracelet.

#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <math.h>
#include <Adafruit_DRV2605.h>
#include "secrets.h"   // defines WIFI_SSID and WIFI_PASSWORD

// ---------- Wi-Fi / network ----------
const int localPort = 4210;

// ---------- Pins & multiplexer ----------
const int ACTION_PIN = 10;  // onboard LED
const int I2C_SDA = 5;
const int I2C_SCL = 4;

const uint8_t TCA_ADDR  = 0x70;
const uint8_t BOTH_MASK = (1 << 6) | (1 << 7);   // channels 6 and 7
const uint8_t DRV_ADDR  = 0x5A;
const uint8_t REG_RTP   = 0x02;                  // real-time playback input

// ---------- Timing ----------
const uint32_t UPDATE_US  = 2000;   // actuator update period: 500 Hz
const uint32_t TIMEOUT_MS = 300;    // no packet for this long -> off (Unity resends at least every 100 ms)

WiFiUDP udp;
Adafruit_DRV2605 drv;

// Latest command from Unity
int   level   = 0;       // 0..127
float depth   = 0.0f;    // 0..1
float rateHz  = 0.0f;
uint32_t lastPacketMs = 0;
bool     everReceived = false;

// Generator state
float    phase = 0.0f;
uint32_t lastUpdateUs = 0;
int      lastWritten = -1;
uint32_t lastIpPrintMs = 0;

// ---------- Multiplexer / register helpers ----------
void tcaSelect(uint8_t mask) {
  Wire.beginTransmission(TCA_ADDR);
  Wire.write(mask);
  Wire.endTransmission();
}

void writeReg(uint8_t reg, uint8_t val) {
  Wire.beginTransmission(DRV_ADDR);
  Wire.write(reg);
  Wire.write(val);
  Wire.endTransmission();
}

uint8_t readReg(uint8_t reg) {
  Wire.beginTransmission(DRV_ADDR);
  Wire.write(reg);
  Wire.endTransmission(false);
  Wire.requestFrom((int)DRV_ADDR, 1);
  return Wire.available() ? Wire.read() : 0;
}

// ---------- LRA setup (same as beetle_wifi_unity.ino) ----------
void configureWidebandLRA() {
  writeReg(0x01, 0x00);                  // out of standby
  writeReg(0x1A, 0x80);                  // N_ERM_LRA = 1 (LRA mode)
  writeReg(0x1D, readReg(0x1D) | 0x01);  // LRA open-loop
  writeReg(0x1D, readReg(0x1D) & ~0x08); // signed RTP data (0..127)
  writeReg(0x20, 60);                    // OL_LRA_PERIOD: 60 x 98.46 us ~ 170 Hz
  writeReg(0x17, 91);                    // OD_CLAMP ~ 2.0 V (21.96 mV per step)
}

bool initDriverOnChannel(uint8_t channel) {
  tcaSelect(1 << channel);
  delay(10);

  if (!drv.begin(&Wire)) {
    Serial.printf("Channel %d: could not find DRV2605!\n", channel);
    return false;
  }

  drv.useLRA();   // sets N_ERM_LRA (must come after begin())
  configureWidebandLRA();

  Serial.printf("Channel %d: FEEDBACK (0x1A) = 0x%02X, LRA mode: %s\n",
                channel, readReg(0x1A), (readReg(0x1A) & 0x80) ? "YES" : "NO");

  drv.setMode(DRV2605_MODE_REALTIME);
  drv.setRealtimeValue(0);
  return true;
}

void setup() {
  Serial.begin(115200);
  pinMode(ACTION_PIN, OUTPUT);

  Wire.begin(I2C_SDA, I2C_SCL);
  Wire.setClock(400000);   // fast mode: a 500 Hz update needs short transactions

  Wire.beginTransmission(TCA_ADDR);
  if (Wire.endTransmission() != 0) {
    Serial.println("ERROR: could not find TCA9548A at 0x70!");
    while (1) delay(1000);
  }
  Serial.println("TCA9548A found at 0x70.");

  // Initialise channels 6 and 7 one at a time so readReg() does not collide
  bool ok6 = initDriverOnChannel(6);
  bool ok7 = initDriverOnChannel(7);
  if (!ok6 && !ok7) {
    Serial.println("ERROR: neither DRV2605 initialised on channel 6 or 7.");
    while (1) delay(1000);
  }

  // From here on both channels stay selected: one register write reaches both actuators.
  tcaSelect(BOTH_MASK);

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  WiFi.setSleep(false);  // avoid power-save packet bunching
  Serial.println("\nConnected! IP: " + WiFi.localIP().toString() + "  (put this in Unity's Esp32 IP)");
  udp.begin(localPort);

  lastUpdateUs = micros();
}

void loop() {
  // 1. Drain the socket; the newest valid packet wins.
  while (udp.parsePacket() > 0) {
    uint8_t buf[8];
    int n = udp.read(buf, sizeof(buf));
    if (n >= 1) {
      level = constrain((int)buf[0], 0, 127);
      if (n >= 3) {
        depth  = buf[1] / 255.0f;
        rateHz = buf[2] * 0.5f;
      } else {
        depth  = 0.0f;   // plain level packet: no beating
        rateHz = 0.0f;
      }
      lastPacketMs = millis();
      everReceived = true;
    }
  }

  // 2. Safety: stale or never-received -> off.
  bool alive = everReceived && (millis() - lastPacketMs) < TIMEOUT_MS;

  // 3. Generate and write at a fixed rate, using the measured time step.
  uint32_t nowUs = micros();
  if ((uint32_t)(nowUs - lastUpdateUs) >= UPDATE_US) {
    float dt = (uint32_t)(nowUs - lastUpdateUs) * 1e-6f;
    lastUpdateUs = nowUs;

    phase += rateHz * dt;
    phase -= floorf(phase);
    // Raised cosine: peaks keep the level, dips go down by `depth`. Never above the level.
    float mod = 1.0f - depth * 0.5f * (1.0f - cosf(2.0f * (float)M_PI * phase));
    int out = alive ? (int)lroundf(level * mod) : 0;
    out = constrain(out, 0, 127);

    if (out != lastWritten) {
      writeReg(REG_RTP, (uint8_t)out);   // both actuators (mux has both channels open)
      lastWritten = out;
      digitalWrite(ACTION_PIN, out > 10 ? HIGH : LOW);
    }
  }

  // 4. Every 10 s print the IP, handy because the hotspot can hand out a new one.
  if (millis() - lastIpPrintMs > 10000) {
    lastIpPrintMs = millis();
    Serial.println("IP: " + WiFi.localIP().toString() + (alive ? "  (receiving)" : "  (no packets)"));
  }
}
