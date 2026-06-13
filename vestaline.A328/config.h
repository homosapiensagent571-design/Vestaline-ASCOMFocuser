#ifndef CONFIG_H
#define CONFIG_H

// ============================================================
// 引脚定义 (Pin Definitions)
// ============================================================

// ULN2003 步进电机驱动 (4 相)
#define PIN_STEPPER_IN1  4    // PD4 - 蓝色 / Phase A
#define PIN_STEPPER_IN2  5    // PD5 - 粉色 / Phase B
#define PIN_STEPPER_IN3  6    // PD6 - 黄色 / Phase C
#define PIN_STEPPER_IN4  7    // PD7 - 橙色 / Phase D

// NTC 热敏电阻
#define PIN_NTC          A0   // ADC0 - NTC 分压输入
#define NTC_R_SERIES     10000.0f  // 串联固定电阻 10kΩ
#define NTC_R_REF        10000.0f  // NTC 标称电阻 @25°C 10kΩ
#define NTC_BETA         3950.0f   // NTC B 系数 (K)
#define NTC_T_REF        298.15f   // 参考温度 25°C (K)
#define ADC_REF_VOLTAGE  5.0f      // ADC 参考电压
#define ADC_MAX          1023.0f   // 10-bit ADC 最大值
#define TEMP_OVERSAMPLE  8         // 温度过采样次数

// 可选 IO
#define PIN_LED_STATUS   13   // PB5 - 工作状态指示灯 (板载LED)
#define PIN_DEBUG_TX     8    // PB0 - 调试串口 TX (接 USB-TTL 模块)
#define DEBUG_BAUD       115200

// ============================================================
// 电机参数 (Motor Parameters)
// ============================================================

#define MOTOR_STEPS_PER_REV  64    // 电机固有步数/转 (5.625°/步, 28BYJ-48)
#define GEAR_RATIO           32    // 机械减速比 (实测校准: 4096步/转)
#define SCREW_PITCH_MM       20.0f // 丝杆螺距 mm/转

// 输出轴步数
#define OUTPUT_STEPS_FULL    (MOTOR_STEPS_PER_REV * GEAR_RATIO)       // 2048
#define OUTPUT_STEPS_HALF    (MOTOR_STEPS_PER_REV * GEAR_RATIO * 2)   // 4096

// 单步位移 (mm)
#define STEP_MM_FULL         (SCREW_PITCH_MM / OUTPUT_STEPS_FULL)    // ~0.00977
#define STEP_MM_HALF         (SCREW_PITCH_MM / OUTPUT_STEPS_HALF)   // ~0.00488

// ============================================================
// 运动参数 (Motion Parameters)
// ============================================================

// 默认步进模式: 0=全步(full), 1=半步(half)
#define DEFAULT_STEP_MODE    1     // 默认半步

// 速度参数 (μs/step)
#define SPEED_MIN_US         2000  // 最慢速度  500 步/秒
#define SPEED_MAX_US         1500  // 最快速度  666 步/秒
#define DEFAULT_SPEED_US     1667  // 默认速度  600 步/秒

// 加减速参数
#define ACCEL_STEPS          256   // 加速段步数 (使能时)
#define ACCEL_ENABLED        1     // 默认启用加减速

// 默认最大行程
#define DEFAULT_MAX_STEPS    16384 // 4转×4096步 = 80mm

// ============================================================
// 通讯参数 (Communication)
// ============================================================

#define SERIAL_BAUD          9600   // Vestaline 标准波特率
#define SERIAL_TIMEOUT_MS    1000
#define CMD_BUFFER_SIZE      64
#define LINE_TERMINATOR      '\n'

// ============================================================
// EEPROM 地址分配 (ATmega328P: 1024 bytes)
// ============================================================

#define EE_ADDR_MAGIC        0     // 魔数 (2 bytes: 0xA5F0)
#define EE_ADDR_VERSION      2     // 配置版本 (2 bytes)
#define EE_ADDR_STEP_MODE    4     // 步进模式 (1 byte)
#define EE_ADDR_MAX_STEPS    5     // 最大行程 (4 bytes, int32)
#define EE_ADDR_SPEED_US     9     // 默认速度 (2 bytes, int16)
#define EE_ADDR_ACCEL_EN     11    // 加减速使能 (1 byte)
#define EE_ADDR_RESERVED_1    12    // 预留 (1 byte, 原HOME_DIR)
#define EE_ADDR_NTC_BETA     13    // NTC B 系数 (4 bytes, float)
#define EE_ADDR_NTC_R_REF    17    // NTC 参考电阻 (4 bytes, float)
#define EE_ADDR_NTC_SERIES   21    // 串联电阻校准 (4 bytes, float)
#define EE_ADDR_LAST_POS     25    // 上次位置 (4 bytes, int32)
#define EE_CONFIG_VERSION    1
#define EE_MAGIC             0xA5F0

// ============================================================
// 调试配置 (Debug)
// ============================================================

#define DEBUG_NONE           0     // 无调试输出
#define DEBUG_BASIC          1     // 基本：关键事件
#define DEBUG_VERBOSE        2     // 详细：每步输出
#define DEFAULT_DEBUG_LEVEL  DEBUG_BASIC

#endif // CONFIG_H
