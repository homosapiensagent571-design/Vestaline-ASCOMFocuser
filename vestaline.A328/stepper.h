#ifndef STEPPER_H
#define STEPPER_H

#include <Arduino.h>
#include <SoftwareSerial.h>
#include "config.h"

extern SoftwareSerial debugSerial;

// ============================================================
// 28BYJ-48 + ULN2003 半步序列 (Half-Step Sequence)
// 4 相 8 拍: A → AB → B → BC → C → CD → D → DA
// ============================================================

// 半步模式 (8 拍/循环)
const uint8_t HALF_STEP_SEQ[8][4] = {
  {1, 0, 0, 0},  // 0: A
  {1, 1, 0, 0},  // 1: AB
  {0, 1, 0, 0},  // 2: B
  {0, 1, 1, 0},  // 3: BC
  {0, 0, 1, 0},  // 4: C
  {0, 0, 1, 1},  // 5: CD
  {0, 0, 0, 1},  // 6: D
  {1, 0, 0, 1},  // 7: DA
};

// 全步模式 (4 拍/循环) — 双相通电，高扭矩
const uint8_t FULL_STEP_SEQ[4][4] = {
  {1, 1, 0, 0},  // AB
  {0, 1, 1, 0},  // BC
  {0, 0, 1, 1},  // CD
  {1, 0, 0, 1},  // DA
};

// ============================================================
// 步进电机状态机
// ============================================================

enum StepperState {
  STEPPER_IDLE,      // 空闲
  STEPPER_ACCEL,     // 加速段
  STEPPER_COAST,     // 匀速段
  STEPPER_DECEL,     // 减速段
};

// ============================================================
// Stepper 类
// ============================================================

class Stepper {
public:
  Stepper();

  void begin();
  void setSpeed(uint16_t speedUsMin, uint16_t speedUsMax);
  void setAccel(uint16_t accelSteps, bool enabled);
  void setStepMode(uint8_t mode); // 0=full, 1=half

  void move(int32_t steps);
  void moveTo(int32_t position);
  void halt();

  void tick(); // 在 loop() 中周期性调用

  int32_t getPosition() const { return _position; }
  int32_t getTarget()   const { return _target; }
  void    setTarget(int32_t target) { _target = target; }
  bool    isMoving()    const { return _state != STEPPER_IDLE; }
  uint8_t getState()    const { return _state; }
  int32_t getMaxSteps() const { return _maxSteps; }
  uint8_t getStepMode() const { return _stepMode; }
  uint16_t getSpeedMin() const { return _speedMinUs; }
  uint16_t getSpeedMax() const { return _speedMaxUs; }

  void setPosition(int32_t pos) { _position = pos; }
  void setMaxSteps(int32_t maxSteps);
  void setDebugLevel(uint8_t level) { _debugLevel = level; }

private:
  void _stepForward();
  void _stepBackward();
  void _setPhase(uint8_t phase);
  void _phaseOff();

  // 引脚
  uint8_t _pins[4];

  // 步进序列
  uint8_t _seqIndex;       // 当前序列索引
  uint8_t _seqLen;         // 序列长度 (4 全步 / 8 半步)
  uint8_t _stepMode;       // 0=全步, 1=半步

  // 位置
  int32_t _position;       // 当前位置 (steps)
  int32_t _target;         // 目标位置
  int32_t _maxSteps;       // 最大行程

  // 速度
  uint16_t _speedMinUs;    // 最慢速度 (μs/step)
  uint16_t _speedMaxUs;    // 最快速度 (μs/step)
  uint16_t _currentSpeedUs;// 当前速度

  // 加减速
  uint16_t _accelSteps;    // 加速/减速段步数
  bool     _accelEnabled;  // 加减速使能
  uint16_t _accelCount;    // 加速步计数
  uint16_t _decelStart;    // 开始减速的剩余步数

  // 状态
  StepperState _state;
  int32_t _remaining;      // 剩余步数
  int8_t  _direction;      // 方向: +1 正转, -1 反转
  unsigned long _lastStepUs;

  // 调试
  uint8_t _debugLevel;
};

#endif // STEPPER_H
