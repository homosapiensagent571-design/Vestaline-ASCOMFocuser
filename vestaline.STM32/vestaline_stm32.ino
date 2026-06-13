// ============================================================
//  VestalFocuser beta 0.6.10 — STM32 + A4988 Firmware
//  Target:  STM32F103C6T6 (Blue Pill, Arduino_STM32)
//  Driver:  A4988 (STEP/DIR, 16 microsteps, MS1-3 = HIGH)
//  Motor:   NEMA17 200 step/rev → 4096 step/rev with 16x microstep
//  Sensor:  NTC 10k Thermistor (B=3950)
//  Protocol: Vestaline :XXxxxx# (100% compatible with ASCOM/INDI)
// ============================================================

#define DEBUG_ENABLE
// ============================================================

#include "config.h"
#include "stepper_a4988.h"
#include "thermistor.h"

StepperA4988 motor;
Thermistor   tempSensor;

// STM32 无 EEPROM, 位置不持久化 (断电归零)
// 如需持久化: 外接 EEPROM 或使用 Flash 模拟

// 调试串口 (PA2 TX → USB-TTL RX)
HardwareSerial DebugSerial(PIN_DEBUG_TX, NC);  // USART2

#ifdef DEBUG_ENABLE
#define DBG(msg)           DebugSerial.println(F(msg))
#define DBG2(msg, v)       do { DebugSerial.print(F(msg)); DebugSerial.println(v); } while(0)
#else
#define DBG(msg)           ((void)0)
#define DBG2(msg, v)       ((void)0)
#endif

// 串口接收缓冲
const uint8_t CMD_BUF_SIZE = 16;
char  cmdBuf[CMD_BUF_SIZE];
uint8_t cmdLen = 0;

// LED 定时
unsigned long ledTimer = 0;

// ============================================================
// setup()
// ============================================================

void setup()
{
    pinMode(PIN_LED_STATUS, OUTPUT);
    digitalWrite(PIN_LED_STATUS, LOW);  // PC13 低电平点亮

    Serial.begin(SERIAL_BAUD);
    while (!Serial);

    DebugSerial.begin(DEBUG_BAUD);
    DBG(F("VestalFocuser STM32 v0.6.10 booting..."));

    motor.begin();
    tempSensor.begin();

    DBG2(F("MaxSteps="), motor.getMaxSteps());
    DBG(F("A4988 16-microstep mode"));
    DBG(F("No EEPROM — position resets on power cycle"));
    DBG(F("Ready."));

    startupBlink();
    ledTimer = millis();
}

void startupBlink()
{
    for (uint8_t i = 0; i < 3; i++) {
        digitalWrite(PIN_LED_STATUS, HIGH);   // LED 灭 (PC13 高电平)
        delay(100);
        digitalWrite(PIN_LED_STATUS, LOW);    // LED 亮
        delay(100);
    }
}

// ============================================================
// loop()
// ============================================================

void loop()
{
    motor.tick();
    updateLED();
    receiveCommand();
}

// ============================================================
// LED 状态指示
// ============================================================

void updateLED()
{
    unsigned long now = millis();
    unsigned long elapsed = now - ledTimer;

    if (motor.isMoving()) {
        // 快闪: 100ms 亮 / 100ms 灭
        digitalWrite(PIN_LED_STATUS, (elapsed % 200) < 100 ? LOW : HIGH);
    } else {
        // 慢闪: 200ms 亮 / 1800ms 灭
        digitalWrite(PIN_LED_STATUS, (elapsed % 2000) < 200 ? LOW : HIGH);
    }
}

// ============================================================
// 串口接收 (Vestaline 协议: :XXXX...XX#)
// ============================================================

void receiveCommand()
{
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

void parseCommand(const char* cmd)
{
    DBG2(F("CMD: :"), cmd);

    uint8_t offset = 0;
    if (cmd[0] == '2') offset = 1;  // 双电机前缀

    switch (cmd[offset]) {
        case 'C':
        {
            float t = tempSensor.readTemperature();
            DBG2(F("  Temp="), t);
            sendHex16((int16_t)(t * 2.0f));
        }
        break;

        case 'F':
            if (cmd[offset + 1] == 'G') {
                DBG(F("  StartMove"));
                motorStart();
            } else if (cmd[offset + 1] == 'Q') {
                DBG(F("  Halt"));
                motor.halt();
            }
            break;

        case 'G':
            switch (cmd[offset + 1]) {
                case 'I':
                    sendHex8(motor.isMoving() ? 1 : 0);
                    break;
                case 'N':
                    sendHex16((uint16_t)motor.getTarget());
                    break;
                case 'P':
                    sendHex16((uint16_t)motor.getPosition());
                    break;
                case 'T':
                {
                    float t = tempSensor.readTemperature();
                    DBG2(F("  Temp="), t);
                    sendHex16((int16_t)(t * 2.0f));
                }
                break;
                case 'V':
                    sendVersion();
                    break;
                default:
                    sendHex8(0);
                    break;
            }
            break;

        case 'S':
            switch (cmd[offset + 1]) {
                case 'N':
                {
                    uint32_t tgt = hex2u32(cmd + offset + 2);
                    DBG2(F("  SetTarget="), tgt);
                    motor.setTarget(tgt);
                }
                break;
                case 'P':
                {
                    uint32_t pos = hex2u32(cmd + offset + 2);
                    DBG2(F("  SetPos="), pos);
                    motor.setPosition(pos);
                }
                break;
                default:
                    break;
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

void motorStart()
{
    if (motor.isMoving()) {
        DBG(F("  Busy, ignored"));
        return;
    }

    int32_t target = motor.getTarget();
    int32_t pos    = motor.getPosition();
    int32_t delta  = target - pos;

    DBG2(F("  Pos="), pos);
    DBG2(F("  Target="), target);
    DBG2(F("  Delta="), delta);

    if (delta == 0) {
        DBG(F("  Already at target"));
        return;
    }

    motor.move(delta);
    DBG(F("  Moving..."));
}

// ============================================================
// 响应工具 (与 A328 版本完全兼容)
// ============================================================

void sendHex8(uint8_t val)
{
    char buf[4];
    snprintf(buf, sizeof(buf), "%02X", val);
    Serial.print(buf);
    Serial.print('#');
}

void sendHex16(uint16_t val)
{
    char buf[6];
    snprintf(buf, sizeof(buf), "%04X", val);
    Serial.print(buf);
    Serial.print('#');
}

void sendVersion()
{
    Serial.print(F("10:01#"));
}

// ============================================================
// Hex 解析
// ============================================================

uint8_t hexNibble(char c)
{
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    return 0;
}

uint16_t hex2u16(const char* s)
{
    return ((uint16_t)hexNibble(s[0]) << 12) |
           ((uint16_t)hexNibble(s[1]) << 8)  |
           ((uint16_t)hexNibble(s[2]) << 4)  |
            (uint16_t)hexNibble(s[3]);
}

uint32_t hex2u32(const char* s)
{
    return ((uint32_t)hexNibble(s[0]) << 28) |
           ((uint32_t)hexNibble(s[1]) << 24) |
           ((uint32_t)hexNibble(s[2]) << 20) |
           ((uint32_t)hexNibble(s[3]) << 16) |
           ((uint32_t)hexNibble(s[4]) << 12) |
           ((uint32_t)hexNibble(s[5]) << 8)  |
           ((uint32_t)hexNibble(s[6]) << 4)  |
            (uint32_t)hexNibble(s[7]);
}
