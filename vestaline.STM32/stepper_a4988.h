#ifndef STEPPER_A4988_H
#define STEPPER_A4988_H

#include <Arduino.h>
#include "config.h"

extern HardwareSerial *debugSerial;

// ============================================================
// A4988 步进电机驱动
// 使用 STEP/DIR 接口, MS1-MS3 硬件微步
// 16 微步模式: MS1=MS2=MS3=HIGH
// ============================================================

enum StepperState {
    STEPPER_IDLE,
    STEPPER_ACCEL,
    STEPPER_COAST,
    STEPPER_DECEL,
};

class StepperA4988 {
public:
    StepperA4988();

    void begin();
    void setSpeed(uint16_t speedUsMin, uint16_t speedUsMax);
    void setAccel(uint16_t accelSteps, bool enabled);

    void moveTo(int32_t position);
    void move(int32_t steps);
    void halt();

    void tick();  // loop() 中周期性调用

    int32_t getPosition() const { return _position; }
    int32_t getTarget()   const { return _target; }
    void    setTarget(int32_t target) { _target = target; }
    bool    isMoving()    const { return _state != STEPPER_IDLE; }
    int32_t getMaxSteps() const { return _maxSteps; }

    void setPosition(int32_t pos) { _position = pos; }
    void setMaxSteps(int32_t maxSteps);
    void setDebugLevel(uint8_t level) { _debugLevel = level; }

private:
    void _stepPulse();
    void _enable(bool on);

    uint8_t _pinStep, _pinDir, _pinEnable;
    uint8_t _pinMS1, _pinMS2, _pinMS3;

    int32_t _position;
    int32_t _target;
    int32_t _maxSteps;
    int32_t _remaining;
    int8_t  _direction;

    uint16_t _speedMinUs;
    uint16_t _speedMaxUs;
    uint16_t _currentSpeedUs;

    uint16_t _accelSteps;
    bool     _accelEnabled;
    uint16_t _accelCount;

    StepperState _state;
    unsigned long _lastStepUs;
    uint8_t _debugLevel;
};

#endif // STEPPER_A4988_H
