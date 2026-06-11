// ============================================================
//  VestalFocuser beta 0.6.10 — Vestaline Compatible Firmware
//  Target:  Arduino Nano ATmega328P
//  Driver:  ULN2003 + 28BYJ-48 (64-step, 5.625deg)
//  Screw:   20mm/rev, Calibrated: 4096 steps/rev, 16384 max
//  Sensor:  NTC 10k Thermistor (B=3950)
//  Protocol: Vestaline :XXxxxx#
//
//  调试: 注释下行关闭调试输出
// ============================================================

#define DEBUG_ENABLE
// ============================================================

#include "config.h"
#include "stepper.h"
#include "thermistor.h"
#include <EEPROM.h>
#include <SoftwareSerial.h>

Stepper     motor;
Thermistor  tempSensor;

// 调试串口 (D8 TX → USB-TTL RX, 共 GND)
SoftwareSerial debugSerial(255, PIN_DEBUG_TX);  // RX=未用, TX=D8

// 调试宏: 仅在调试串口输出, 不干扰 Vestaline 主线
#ifdef DEBUG_ENABLE
#define DBG(msg)   debugSerial.println(F(msg))
#define DBG2(msg,v) do { debugSerial.print(F(msg)); debugSerial.println(v); } while(0)
#define DBGH(v)    do { debugSerial.print(F("0x")); debugSerial.println(v, HEX); } while(0)
#else
#define DBG(msg)   ((void)0)
#define DBG2(msg,v) ((void)0)
#define DBGH(v)    ((void)0)
#endif

// EEPROM 地址 (与 firmware6.9 共用, 保持兼容)
#define EE_MAGIC_VAL  0xA5F0
#define EE_ADDR_MAGIC     0
#define EE_ADDR_STEPMODE  4
#define EE_ADDR_LASTPOS   25

// 串口接收缓冲
const uint8_t CMD_BUF_SIZE = 16;
char  cmdBuf[CMD_BUF_SIZE];
uint8_t cmdLen = 0;

// LED 状态定时
unsigned long ledTimer = 0;

// ============================================================
// setup()
// ============================================================

void setup() {
  pinMode(PIN_LED_STATUS, OUTPUT);
  digitalWrite(PIN_LED_STATUS, HIGH);

  Serial.begin(SERIAL_BAUD);
  while (!Serial);

  debugSerial.begin(DEBUG_BAUD);
  DBG("VestalFocuser FW v0.6.10 booting...");

  motor.begin();
  tempSensor.begin();

  // 从 EEPROM 恢复状态
  uint16_t magic;
  EEPROM.get(EE_ADDR_MAGIC, magic);
  if (magic == EE_MAGIC_VAL) {
    DBG("EEPROM: valid, restoring config");

    uint8_t mode;
    EEPROM.get(EE_ADDR_STEPMODE, mode);
    motor.setStepMode(mode);
    DBG2("  StepMode=", mode);

    int32_t lastPos;
    EEPROM.get(EE_ADDR_LASTPOS, lastPos);
    if (lastPos >= 0 && lastPos <= motor.getMaxSteps()) {
      motor.setPosition(lastPos);
      DBG2("  LastPos=", lastPos);
    }
  } else {
    DBG("EEPROM: blank, using defaults");
    EEPROM.put(EE_ADDR_MAGIC, EE_MAGIC_VAL);
    EEPROM.put(EE_ADDR_STEPMODE, (uint8_t)DEFAULT_STEP_MODE);
  }

  DBG2("MaxSteps=", motor.getMaxSteps());
  DBG2("StepMode=", motor.getStepMode());
  DBG2("Baud=", SERIAL_BAUD);
  DBG("Ready.");

  startupBlink();
  ledTimer = millis();
}

void startupBlink() {
  digitalWrite(PIN_LED_STATUS, LOW);
  delay(200);
  for (uint8_t i = 0; i < 3; i++) {
    digitalWrite(PIN_LED_STATUS, HIGH);
    delay(100);
    digitalWrite(PIN_LED_STATUS, LOW);
    delay(100);
  }
}

// ============================================================
// loop()
// ============================================================

void loop() {
  motor.tick();
  updateLED();
  receiveCommand();

  // 运动完成时保存位置到 EEPROM
  static bool wasMoving = false;
  if (wasMoving && !motor.isMoving()) {
    DBG2("MoveDone, pos=", motor.getPosition());
    EEPROM.put(EE_ADDR_LASTPOS, motor.getPosition());
  }
  wasMoving = motor.isMoving();
}

// ============================================================
// LED 状态指示
// ============================================================

void updateLED() {
  unsigned long now = millis();
  unsigned long elapsed = now - ledTimer;

  if (motor.isMoving()) {
    // 快闪: 100ms 亮 / 100ms 灭
    digitalWrite(PIN_LED_STATUS, (elapsed % 200) < 100 ? HIGH : LOW);
  } else {
    // 慢闪: 200ms 亮 / 1800ms 灭 (空闲指示灯)
    digitalWrite(PIN_LED_STATUS, (elapsed % 2000) < 200 ? HIGH : LOW);
  }
}

// ============================================================
// 串口接收
// ============================================================

void receiveCommand() {
  while (Serial.available() > 0) {
    char c = Serial.read();
    if (c == ':') {
      cmdLen = 0;
    } else if (c == '#') {
      if (cmdLen > 0) {
        cmdBuf[cmdLen] = '\0';
        parseCommand(cmdBuf);
        cmdLen = 0;
      }
    } else if (cmdLen < CMD_BUF_SIZE - 1) {
      cmdBuf[cmdLen++] = c;
    }
  }
}

// ============================================================
// 命令解析
// ============================================================

void parseCommand(const char* cmd) {
  DBG2("CMD: :", cmd);

  uint8_t offset = 0;
  if (cmd[0] == '2') offset = 1;  // 双电机前缀

  switch (cmd[offset]) {
    case 'C':
      {
        float t = tempSensor.readTemperature();
        DBG2("  UpdateTemp=", t);
        sendHex16( (int16_t)(t * 2.0f) );
      }
      break;

    case 'F':
      if (cmd[offset + 1] == 'G') {
        DBG("  StartMove");
        motorStart();
      } else if (cmd[offset + 1] == 'Q') {
        DBG("  Halt");
        motor.halt();
      }
      break;

    case 'G':
      switch (cmd[offset + 1]) {
        case 'D': sendHex8( motor.getSpeedMax() / 10 ); break;
        case 'H': sendHex8( motor.getStepMode() );       break;
        case 'I': sendHex8( motor.isMoving() ? 1 : 0 );  break;
        case 'N': sendHex16( (uint16_t)motor.getTarget() ); break;
        case 'P': sendHex16( (uint16_t)motor.getPosition() ); break;
        case 'T':
          {
            float t = tempSensor.readTemperature();
            DBG2("  Temp=", t);
            sendHex16( (int16_t)(t * 2.0f) );
          }
          break;
        case 'V': sendVersion(); break;
        default:  sendHex8(0); break;
      }
      break;

    case 'S':
      switch (cmd[offset + 1]) {
        case 'D':
          {
            uint16_t spd = hex2u16(cmd + offset + 2) * 10;
            DBG2("  SetSpeed=", spd);
            motor.setSpeed(SPEED_MIN_US, spd);
          }
          break;
        case 'F':
          DBG("  SetMode=Full");
          motor.setStepMode(0);
          EEPROM.put(EE_ADDR_STEPMODE, (uint8_t)0);
          break;
        case 'H':
          DBG("  SetMode=Half");
          motor.setStepMode(1);
          EEPROM.put(EE_ADDR_STEPMODE, (uint8_t)1);
          break;
        case 'N':
          {
            uint32_t tgt = hex2u32(cmd + offset + 2);
            DBG2("  SetTarget=", tgt);
            motor.setTarget(tgt);
          }
          break;
        case 'P':
          {
            uint32_t pos = hex2u32(cmd + offset + 2);
            DBG2("  SetPos=", pos);
            motor.setPosition(pos);
            EEPROM.put(EE_ADDR_LASTPOS, motor.getPosition());
          }
          break;
        default: break;
      }
      break;

    default:
      sendHex8(0);
      break;
  }
}

// ============================================================
// 移动控制
// ============================================================

void motorStart() {
  if (motor.isMoving()) {
    DBG("  Busy, ignored");
    return;
  }

  int32_t target = motor.getTarget();
  int32_t pos    = motor.getPosition();
  int32_t delta  = target - pos;

  DBG2("  Pos=", pos);
  DBG2("  Target=", target);
  DBG2("  Delta=", delta);

  if (delta == 0) {
    DBG("  Already at target");
    return;
  }

  motor.move(delta);
  DBG("  Moving...");
}

// ============================================================
// 响应工具
// ============================================================

void sendHex8(uint8_t val) {
  char buf[4];
  snprintf_P(buf, sizeof(buf), PSTR("%02X"), val);
  Serial.print(buf);
  Serial.print('#');
}

void sendHex16(uint16_t val) {
  char buf[6];
  snprintf_P(buf, sizeof(buf), PSTR("%04X"), val);
  Serial.print(buf);
  Serial.print('#');
}

void sendVersion() {
  Serial.print(F("10:01#"));
}

// ============================================================
// Hex 解析
// ============================================================

uint8_t hexNibble(char c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  return 0;
}

uint8_t hex2u8(const char* s) {
  return (hexNibble(s[0]) << 4) | hexNibble(s[1]);
}

uint16_t hex2u16(const char* s) {
  return ((uint16_t)hexNibble(s[0]) << 12) |
         ((uint16_t)hexNibble(s[1]) << 8)  |
         ((uint16_t)hexNibble(s[2]) << 4)  |
          (uint16_t)hexNibble(s[3]);
}

uint32_t hex2u32(const char* s) {
  return ((uint32_t)hexNibble(s[0]) << 28) |
         ((uint32_t)hexNibble(s[1]) << 24) |
         ((uint32_t)hexNibble(s[2]) << 20) |
         ((uint32_t)hexNibble(s[3]) << 16) |
         ((uint32_t)hexNibble(s[4]) << 12) |
         ((uint32_t)hexNibble(s[5]) << 8)  |
         ((uint32_t)hexNibble(s[6]) << 4)  |
          (uint32_t)hexNibble(s[7]);
}
