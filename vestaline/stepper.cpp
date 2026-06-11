#include "stepper.h"

Stepper::Stepper()
  : _seqIndex(0)
  , _seqLen(8)
  , _stepMode(DEFAULT_STEP_MODE)
  , _position(0)
  , _target(0)
  , _maxSteps(DEFAULT_MAX_STEPS)
  , _speedMinUs(SPEED_MIN_US)
  , _speedMaxUs(SPEED_MAX_US)
  , _currentSpeedUs(DEFAULT_SPEED_US)
  , _accelSteps(ACCEL_STEPS)
  , _accelEnabled(ACCEL_ENABLED)
  , _accelCount(0)
  , _decelStart(0)
  , _state(STEPPER_IDLE)
  , _remaining(0)
  , _direction(1)
  , _lastStepUs(0)
  , _debugLevel(DEFAULT_DEBUG_LEVEL)
{
  _pins[0] = PIN_STEPPER_IN1;
  _pins[1] = PIN_STEPPER_IN2;
  _pins[2] = PIN_STEPPER_IN3;
  _pins[3] = PIN_STEPPER_IN4;
}

void Stepper::begin() {
  for (uint8_t i = 0; i < 4; i++) {
    pinMode(_pins[i], OUTPUT);
    digitalWrite(_pins[i], LOW);
  }
  _seqLen = (_stepMode == 0) ? 4 : 8;
  if (_debugLevel >= DEBUG_VERBOSE) {
    debugSerial.print(F("[DBG] Stepper init: mode="));
    debugSerial.print(_stepMode ? F("HALF") : F("FULL"));
    debugSerial.print(F(" maxSteps="));
    debugSerial.print(_maxSteps);
    debugSerial.print(F(" seqLen="));
    debugSerial.println(_seqLen);
  }
}

void Stepper::setSpeed(uint16_t speedUsMin, uint16_t speedUsMax) {
  _speedMinUs = speedUsMin;
  _speedMaxUs = speedUsMax;
  _currentSpeedUs = speedUsMax; // default to max
}

void Stepper::setAccel(uint16_t accelSteps, bool enabled) {
  _accelSteps = accelSteps;
  _accelEnabled = enabled;
}

void Stepper::setStepMode(uint8_t mode) {
  if (_state != STEPPER_IDLE) return;
  _stepMode = mode;
  _seqLen = (mode == 0) ? 4 : 8;
  _seqIndex = 0;
}

void Stepper::setMaxSteps(int32_t maxSteps) {
  if (maxSteps > 0 && maxSteps <= 32767) {
    _maxSteps = maxSteps;
    if (_position > _maxSteps) _position = _maxSteps;
  }
}

void Stepper::move(int32_t steps) {
  if (_state != STEPPER_IDLE) {
    if (_debugLevel >= DEBUG_BASIC) {
      debugSerial.println(F("[DBG] Stepper busy, command ignored"));
    }
    return;
  }
  if (steps == 0) return;

  int32_t newTarget = _position + steps;
  if (newTarget < 0) newTarget = 0;
  if (newTarget > _maxSteps) newTarget = _maxSteps;

  moveTo(newTarget);
}

void Stepper::moveTo(int32_t position) {
  if (_state != STEPPER_IDLE) {
    if (_debugLevel >= DEBUG_BASIC) {
      debugSerial.println(F("[DBG] Stepper busy, command ignored"));
    }
    return;
  }

  if (position < 0) position = 0;
  if (position > _maxSteps) position = _maxSteps;

  int32_t delta = position - _position;
  if (delta == 0) return;

  _target = position;
  _remaining = abs(delta);
  _direction = (delta > 0) ? 1 : -1;

  if (_debugLevel >= DEBUG_BASIC) {
    debugSerial.print(F("[DBG] Move: pos="));
    debugSerial.print(_position);
    debugSerial.print(F(" -> target="));
    debugSerial.print(_target);
    debugSerial.print(F(" remaining="));
    debugSerial.print(_remaining);
    debugSerial.print(F(" dir="));
    debugSerial.println(_direction);
  }

  if (_accelEnabled && _remaining > (_accelSteps * 2)) {
    _state = STEPPER_ACCEL;
    _accelCount = 0;
    _decelStart = _remaining - _accelSteps;
    _currentSpeedUs = _speedMinUs;
  } else {
    _state = STEPPER_COAST;
    _currentSpeedUs = _speedMaxUs;
    _decelStart = 0;
  }

  _lastStepUs = micros();
}

void Stepper::halt() {
  _state = STEPPER_IDLE;
  _remaining = 0;
  _target = _position;
  _phaseOff();
  if (_debugLevel >= DEBUG_BASIC) {
    debugSerial.print(F("[DBG] Halted at pos="));
    debugSerial.println(_position);
  }
}

void Stepper::_phaseOff() {
  for (uint8_t i = 0; i < 4; i++) {
    digitalWrite(_pins[i], LOW);
  }
}

void Stepper::tick() {
  if (_state == STEPPER_IDLE) return;

  unsigned long now = micros();
  if (now - _lastStepUs < _currentSpeedUs) return;
  _lastStepUs = now;

  // 执行一步
  if (_direction > 0) {
    _stepForward();
    _position++;
  } else {
    _stepBackward();
    _position--;
  }
  _remaining--;

  // 加速段
  if (_state == STEPPER_ACCEL) {
    _accelCount++;
    uint16_t range = _speedMinUs - _speedMaxUs;
    _currentSpeedUs = _speedMinUs - (uint16_t)((uint32_t)range * _accelCount / _accelSteps);
    if (_accelCount >= _accelSteps) {
      _state = STEPPER_COAST;
      _currentSpeedUs = _speedMaxUs;
    }
  }

  // 减速判断
  if (_state == STEPPER_COAST && _remaining <= _accelSteps && _accelEnabled) {
    _state = STEPPER_DECEL;
    _accelCount = _remaining;
  }

  // 减速段
  if (_state == STEPPER_DECEL) {
    uint16_t range = _speedMinUs - _speedMaxUs;
    _currentSpeedUs = _speedMaxUs + (uint16_t)((uint32_t)range * (_accelSteps - _remaining) / _accelSteps);
    if (_currentSpeedUs > _speedMinUs) _currentSpeedUs = _speedMinUs;
  }

  // 完成
  if (_remaining == 0) {
    _state = STEPPER_IDLE;
    _phaseOff();
    if (_debugLevel >= DEBUG_BASIC) {
      debugSerial.print(F("[DBG] Move done: pos="));
      debugSerial.println(_position);
    }
  }
}

// ---- 内部方法 ----

void Stepper::_stepForward() {
  _seqIndex = (_seqIndex + 1) % _seqLen;
  _setPhase(_seqIndex);
}

void Stepper::_stepBackward() {
  _seqIndex = (_seqIndex == 0) ? (_seqLen - 1) : (_seqIndex - 1);
  _setPhase(_seqIndex);
}

void Stepper::_setPhase(uint8_t phase) {
  if (_stepMode == 0) {
    // 全步模式
    for (uint8_t i = 0; i < 4; i++) {
      digitalWrite(_pins[i], FULL_STEP_SEQ[phase][i]);
    }
  } else {
    // 半步模式
    for (uint8_t i = 0; i < 4; i++) {
      digitalWrite(_pins[i], HALF_STEP_SEQ[phase][i]);
    }
  }
}
