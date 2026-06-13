#ifndef CONFIG_H
#define CONFIG_H

// ============================================================
// STM32F103C6T6 + A4988 引脚定义
// Arduino_STM32 pin mapping (Blue Pill)
// ============================================================

// A4988 步进电机驱动
#define PIN_STEP          PB0   // STEP 脉冲
#define PIN_DIR           PB1   // DIR 方向
#define PIN_ENABLE        PB10  // ENABLE (LOW=使能, HIGH=休眠)
#define PIN_MS1           PB12  // 微步 MS1
#define PIN_MS2           PB13  // 微步 MS2
#define PIN_MS3           PB14  // 微步 MS3

// NTC 热敏电阻 (与 A328 版本共用电路参数)
#define PIN_NTC           PA0   // ADC1_IN0
#define NTC_R_SERIES      10000.0f
#define NTC_R_REF         10000.0f
#define NTC_BETA          3950.0f
#define NTC_T_REF         298.15f
#define ADC_REF_VOLTAGE   3.3f      // STM32 ADC 参考电压
#define ADC_MAX           4095.0f   // 12-bit ADC
#define TEMP_OVERSAMPLE   16        // STM32 12-bit, 可减少过采样

// 状态指示灯 (板载 LED, 低电平点亮)
#define PIN_LED_STATUS    PC13

// 调试串口
#define PIN_DEBUG_TX      PA2    // USART2_TX
#define DEBUG_BAUD        115200

// ============================================================
// 电机参数 (Motor Parameters)
// ============================================================

// 物理电机: 步进角 1.8°/步 (200 步/转)
// A4988 16 微步: 200 × 16 = 3200
// 用户指定: 4096 步/转 (如需校准, 修改 MOTOR_STEPS_PER_REV)
#define MOTOR_STEPS_PER_REV     200     // 物理步数/转
#define MICROSTEP_MODE          16      // A4988 微步数 (1/2/4/8/16)
#define OUTPUT_STEPS_PER_REV    4096    // 输出步数/转 = MOTOR_STEPS_PER_REV × MICROSTEP_MODE

#define SCREW_PITCH_MM          20.0f   // 丝杆螺距 mm/转
#define STEP_MM                 (SCREW_PITCH_MM / OUTPUT_STEPS_PER_REV)

// ============================================================
// 运动参数 (Motion Parameters)
// ============================================================

#define DEFAULT_MAX_STEPS       16384   // 4 转 × 4096 = 80mm
#define SPEED_MIN_US            2000    // 最慢 500 Hz
#define SPEED_MAX_US            600     // 最快 1666 Hz (A4988 可更快)
#define DEFAULT_SPEED_US        1000    // 默认 1000 Hz

// 加减速参数
#define ACCEL_STEPS             256
#define ACCEL_ENABLED           1

// ============================================================
// 通讯参数 (Communication)
// ============================================================

#define SERIAL_BAUD             9600
#define CMD_BUF_LEN             16
#define LINE_TERMINATOR         '\n'

// ============================================================
// 调试配置
// ============================================================

#define DEBUG_NONE              0
#define DEBUG_BASIC             1
#define DEBUG_VERBOSE           2
#define DEFAULT_DEBUG_LEVEL     DEBUG_BASIC

#endif // CONFIG_H
