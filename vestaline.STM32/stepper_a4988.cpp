#include "stepper_a4988.h"

extern HardwareSerial *debugSerial;

#define DBG_PRINT(x) do { if (_debugLevel >= DEBUG_BASIC && debugSerial) { debugSerial->print(x); } } while(0)
#define DBG_PRINTLN(x) do { if (_debugLevel >= DEBUG_BASIC && debugSerial) { debugSerial->println(x); } } while(0)
#define DBG_PRINT2(s,v) do { if (_debugLevel >= DEBUG_BASIC && debugSerial) { debugSerial->print(s); debugSerial->println(v); } } while(0)

StepperA4988::StepperA4988()
    : _pinStep(PIN_STEP), _pinDir(PIN_DIR), _pinEnable(PIN_ENABLE)
    , _pinMS1(PIN_MS1), _pinMS2(PIN_MS2), _pinMS3(PIN_MS3)
    , _position(0), _target(0), _maxSteps(DEFAULT_MAX_STEPS)
    , _remaining(0), _direction(1)
    , _speedMinUs(SPEED_MIN_US), _speedMaxUs(SPEED_MAX_US)
    , _currentSpeedUs(DEFAULT_SPEED_US)
    , _accelSteps(ACCEL_STEPS), _accelEnabled(ACCEL_ENABLED)
    , _accelCount(0)
    , _state(STEPPER_IDLE), _lastStepUs(0)
    , _debugLevel(DEFAULT_DEBUG_LEVEL)
{
}

void StepperA4988::begin()
{
    pinMode(_pinStep,   OUTPUT);
    pinMode(_pinDir,    OUTPUT);
    pinMode(_pinEnable, OUTPUT);
    pinMode(_pinMS1,    OUTPUT);
    pinMode(_pinMS2,    OUTPUT);
    pinMode(_pinMS3,    OUTPUT);

    // 初始状态: 禁用, STEP 低, DIR 低
    digitalWrite(_pinStep,   LOW);
    digitalWrite(_pinDir,    LOW);
    digitalWrite(_pinEnable, HIGH);   // HIGH = 禁用 (休眠省电)

    // 16 微步: MS1=MS2=MS3=HIGH
    digitalWrite(_pinMS1, HIGH);
    digitalWrite(_pinMS2, HIGH);
    digitalWrite(_pinMS3, HIGH);

    DBG_PRINTLN(F("[DBG] A4988 init: 16-microstep mode"));
    DBG_PRINT2(F("[DBG]   MaxSteps="), _maxSteps);
    DBG_PRINT2(F("[DBG]   Speed="), _currentSpeedUs);
    DBG_PRINTLN(F("[DBG]   Accel enabled"));
}

void StepperA4988::setSpeed(uint16_t speedUsMin, uint16_t speedUsMax)
{
    _speedMinUs = speedUsMin;
    _speedMaxUs = speedUsMax;
    _currentSpeedUs = speedUsMax;
}

void StepperA4988::setAccel(uint16_t accelSteps, bool enabled)
{
    _accelSteps = accelSteps;
    _accelEnabled = enabled;
}

void StepperA4988::setMaxSteps(int32_t maxSteps)
{
    if (maxSteps > 0 && maxSteps <= 2147483647) {
        _maxSteps = maxSteps;
        if (_position > _maxSteps) _position = _maxSteps;
    }
}

void StepperA4988::move(int32_t steps)
{
    if (_state != STEPPER_IDLE) {
        DBG_PRINTLN(F("[DBG] Busy, command ignored"));
        return;
    }
    if (steps == 0) return;

    int32_t newTarget = _position + steps;
    if (newTarget < 0)  newTarget = 0;
    if (newTarget > _maxSteps) newTarget = _maxSteps;
    moveTo(newTarget);
}

void StepperA4988::moveTo(int32_t position)
{
    if (_state != STEPPER_IDLE) {
        DBG_PRINTLN(F("[DBG] Busy, command ignored"));
        return;
    }
    if (position < 0) position = 0;
    if (position > _maxSteps) position = _maxSteps;

    int32_t delta = position - _position;
    if (delta == 0) return;

    _target = position;
    _remaining = abs(delta);
    _direction = (delta > 0) ? 1 : -1;

    // 设置方向
    digitalWrite(_pinDir, (_direction > 0) ? HIGH : LOW);
    delayMicroseconds(1);

    // 使能驱动
    _enable(true);

    DBG_PRINT2(F("[DBG] Move: pos="), _position);
    DBG_PRINT2(F("[DBG]   target="), _target);
    DBG_PRINT2(F("[DBG]   remaining="), _remaining);

    // 加减速规划
    if (_accelEnabled && _remaining > (_accelSteps * 2)) {
        _state = STEPPER_ACCEL;
        _accelCount = 0;
        _currentSpeedUs = _speedMinUs;
    } else {
        _state = STEPPER_COAST;
        _currentSpeedUs = _speedMaxUs;
    }

    _lastStepUs = micros();
}

void StepperA4988::halt()
{
    _state = STEPPER_IDLE;
    _remaining = 0;
    _target = _position;
    _enable(false);
    DBG_PRINT2(F("[DBG] Halted at pos="), _position);
}

void StepperA4988::tick()
{
    if (_state == STEPPER_IDLE) return;

    unsigned long now = micros();
    if (now - _lastStepUs < _currentSpeedUs) return;
    _lastStepUs = now;

    // 发出一个 STEP 脉冲
    _stepPulse();

    // 更新位置
    _position += _direction;
    _remaining--;

    // 加速段
    if (_state == STEPPER_ACCEL) {
        _accelCount++;
        uint16_t range = _speedMinUs - _speedMaxUs;
        _currentSpeedUs = _speedMinUs
            - (uint16_t)((uint32_t)range * _accelCount / _accelSteps);
        if (_accelCount >= _accelSteps) {
            _state = STEPPER_COAST;
            _currentSpeedUs = _speedMaxUs;
        }
    }

    // 减速判断
    if (_state == STEPPER_COAST && _remaining <= _accelSteps && _accelEnabled) {
        _state = STEPPER_DECEL;
    }

    // 减速段
    if (_state == STEPPER_DECEL) {
        uint16_t range = _speedMinUs - _speedMaxUs;
        // 剩余步数从 accelSteps 递减到 0, 速度从 max 降到 min
        _currentSpeedUs = _speedMaxUs
            + (uint16_t)((uint32_t)range * (_accelSteps - _remaining) / _accelSteps);
        if (_currentSpeedUs > _speedMinUs) _currentSpeedUs = _speedMinUs;
    }

    // 完成
    if (_remaining == 0) {
        _state = STEPPER_IDLE;
        _enable(false);
        DBG_PRINT2(F("[DBG] Move done: pos="), _position);
    }
}

void StepperA4988::_stepPulse()
{
    // A4988 在 STEP 上升沿触发一步
    // 最小脉冲宽度: HIGH ≥ 1μs
    digitalWrite(_pinStep, HIGH);
    delayMicroseconds(2);
    digitalWrite(_pinStep, LOW);
}

void StepperA4988::_enable(bool on)
{
    digitalWrite(_pinEnable, on ? LOW : HIGH);
}
