#include "thermistor.h"

Thermistor::Thermistor()
  : _pin(PIN_NTC)
  , _beta(NTC_BETA)
  , _rRef(NTC_R_REF)
  , _tRef(NTC_T_REF)
  , _rSeries(NTC_R_SERIES)
  , _oversample(TEMP_OVERSAMPLE)
  , _lastTemp(25.0f)
  , _lastReadMs(0)
{
}

void Thermistor::begin() {
  pinMode(_pin, INPUT);
  analogReference(DEFAULT);
}

float Thermistor::readResistance() {
  uint32_t sum = 0;
  for (uint8_t i = 0; i < _oversample; i++) {
    sum += analogRead(_pin);
    delayMicroseconds(100);
  }
  float avg = (float)sum / _oversample;

  // 电压分压: NTC 接 GND, 固定电阻接 VCC
  // Vout = Vcc * (R_ntc / (R_ntc + R_series))
  // ADC = Vout / Vcc * 1023 = R_ntc / (R_ntc + R_series) * 1023
  // R_ntc = R_series * ADC / (1023 - ADC)

  if (avg >= ADC_MAX) return 1e6f; // 开路 -> 极大电阻
  if (avg <= 0.5f)   return 0.0f;  // 短路

  return _rSeries * avg / (ADC_MAX - avg);
}

float Thermistor::readTemperature() {
  // 500ms 内不重复读取 (减少 ADC 噪声)
  unsigned long now = millis();
  if (now - _lastReadMs < 500) {
    return _lastTemp;
  }
  _lastReadMs = now;

  float rNtc = readResistance();

  // Steinhart-Hart 简化公式:
  // 1/T = 1/T0 + (1/β) * ln(R/R0)
  if (rNtc <= 0.01f) {
    _lastTemp = -99.0f;
    return _lastTemp;
  }

  float steinhart;
  steinhart = log(rNtc / _rRef);
  steinhart /= _beta;
  steinhart += 1.0f / _tRef;
  steinhart = 1.0f / steinhart;

  _lastTemp = steinhart - 273.15f; // K -> °C
  return _lastTemp;
}
