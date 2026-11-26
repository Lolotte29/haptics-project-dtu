#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <math.h>
#include <Adafruit_DRV2605.h>

// ---------- Wi-Fi ----------
const char* ssid     = "LOIC_LAISNEY 5613";
const char* password = "2320t6E*";
const int   localPort = 4210;

// ---------- Pins & Multiplexer ----------
const int ACTION_PIN = 10;  // Onboard LED
const int I2C_SDA = 5;
const int I2C_SCL = 4;

const uint8_t TCA_ADDR  = 0x70;
const uint8_t CH6_MASK  = (1 << 6);              // Channel 6 -> 0x40
const uint8_t CH7_MASK  = (1 << 7);              // Channel 7 -> 0x80
const uint8_t BOTH_MASK = CH6_MASK | CH7_MASK;   // Channels 6 & 7 -> 0xC0

// ---------- Actuator parameters (VLV101040A) ----------
// FILL THESE IN FROM THE VYBRONICS DATASHEET, then double-check the
// register formulas against the DRV2605L datasheet (section 8.5.2.x).
const float LRA_RATED_VRMS  = 1.8f;    // TODO: rated voltage (Vrms) from datasheet
const float LRA_MAX_VPEAK   = 2.5f;    // TODO: max drive voltage (Vpeak) from datasheet
const float LRA_FREQ_HZ     = 170.0f;  // resonant frequency

const uint8_t DRV_ADDR = 0x5A;

WiFiUDP udp;
Adafruit_DRV2605 drv;

// ---------- Multiplexer helper ----------
void tcaSelect(uint8_t mask) {
  Wire.beginTransmission(TCA_ADDR);
  Wire.write(mask);
  Wire.endTransmission();
}

// ---------- Low-level register helpers ----------
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

// ---------- Voltage -> register conversion (DRV2605L datasheet formulas) ----------
uint8_t ratedVoltageReg(float vrms, float fLra) {
  const float sampleTime = 300e-6f;  // default LRA sample time (SAMPLE_TIME = 3)
  float v = vrms / (20.71e-3f * sqrtf(1.0f - (400e-6f + sampleTime) * fLra));
  return (uint8_t)constrain((int)roundf(v), 0, 255);
}

uint8_t overdriveClampReg(float vpeak, float fLra) {
  float v = vpeak / (21.96e-3f * sqrtf(1.0f - fLra * 800e-6f));
  return (uint8_t)constrain((int)roundf(v), 0, 255);
}

// ---------- LRA setup ----------
void configureWidebandLRA() {
  writeReg(0x01, 0x00);                  // out of standby
  writeReg(0x1A, 0x80);                  // N_ERM_LRA = 1 (LRA mode)
  writeReg(0x1D, readReg(0x1D) | 0x01);  // LRA open-loop
  writeReg(0x1D, readReg(0x1D) & ~0x08); // signed RTP data (0..127)
  writeReg(0x20, 60);                    // OL_LRA_PERIOD: 60 x 98.46 us ≈ 170 Hz
  writeReg(0x17, 91);                    // OD_CLAMP ≈ 2.0 V (21.96 mV per step)
}

bool initDriverOnChannel(uint8_t channel) {
  tcaSelect(1 << channel);
  delay(10);

  if (!drv.begin(&Wire)) {
    Serial.printf("Channel %d: Could not find DRV2605!\n", channel);
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

  // Check TCA9548A presence
  Wire.beginTransmission(TCA_ADDR);
  if (Wire.endTransmission() != 0) {
    Serial.println("ERROR: Could not find TCA9548A at 0x70!");
    while (1) delay(1000);
  }
  Serial.println("TCA9548A found at 0x70.");

  // Initialize Channel 6 and Channel 7 individually so readReg() doesn't collide
  bool ok6 = initDriverOnChannel(6);
  bool ok7 = initDriverOnChannel(7);

  if (!ok6 && !ok7) {
    Serial.println("ERROR: Neither DRV2605 initialized on Channel 6 or 7.");
    while (1) delay(1000);
  }

  // Open both channels simultaneously for real-time intensity updates
  tcaSelect(BOTH_MASK);

  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  WiFi.setSleep(false);  // avoid power-save packet bunching
  Serial.println("\nConnected! IP: " + WiFi.localIP().toString());
  udp.begin(localPort);
}

void loop() {
  // Drain the socket and keep only the newest packet
  int latest = -1;
  while (udp.parsePacket()) {
    uint8_t b;
    if (udp.read(&b, 1) == 1) latest = b;
  }

  if (latest >= 0) {
    int intensity = constrain(latest, 0, 127);
    tcaSelect(BOTH_MASK);            // ensure both CH6 and CH7 are selected
    drv.setRealtimeValue(intensity); // sends intensity to both LRAs at once
    digitalWrite(ACTION_PIN, intensity > 10 ? HIGH : LOW);
  }
}