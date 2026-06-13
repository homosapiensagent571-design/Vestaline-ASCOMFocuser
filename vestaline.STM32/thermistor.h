#ifndef THERMISTOR_H
#define THERMISTOR_H

#include <Arduino.h>
#include "config.h"

class Thermistor {
public:
    Thermistor();

    void begin();
    float readResistance();
    float readTemperature();

private:
    uint8_t  _pin;
    float    _beta;
    float    _rRef;
    float    _tRef;
    float    _rSeries;
    uint8_t  _oversample;
    float    _lastTemp;
    unsigned long _lastReadMs;
};

#endif // THERMISTOR_H
