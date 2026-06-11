#ifndef THERMISTOR_H
#define THERMISTOR_H

#include <Arduino.h>
#include "config.h"

class Thermistor {
public:
  Thermistor();

  void begin();
  float readTemperature(); // 返回摄氏度

  void setBeta(float beta)         { _beta = beta; }
  void setRRef(float rRef)         { _rRef = rRef; }
  void setRSeries(float rSeries)   { _rSeries = rSeries; }
  void setOversample(uint8_t n)    { _oversample = n; }

  float getBeta()    const { return _beta; }
  float getRRef()    const { return _rRef; }
  float getRSeries() const { return _rSeries; }

  // 直接读取 NTC 电阻值 (调试用)
  float readResistance();

private:
  uint8_t _pin;
  float   _beta;       // NTC B 系数
  float   _rRef;       // 参考电阻 @25°C
  float   _tRef;       // 参考温度 (K)
  float   _rSeries;    // 串联固定电阻
  uint8_t _oversample;
  float   _lastTemp;   // 上次温度 (缓存)
  uint32_t _lastReadMs;
};

#endif // THERMISTOR_H
