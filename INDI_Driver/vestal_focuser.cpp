/*
  VestalFocuser — INDI Focuser Driver
  Vestaline protocol for Arduino Nano + 28BYJ-48

  Copyright(c) 2026 Vestaline
  Based on INDI skeleton focuser driver by Jasem Mutlaq.

  License: LGPL 2.1
*/

#include "vestal_focuser.h"
#include "indicom.h"

#include <cstring>
#include <memory>
#include <thread>
#include <chrono>

static std::unique_ptr<VestalFocuser> vestalFocuser(new VestalFocuser());

// Vestaline protocol constants
#define VESTALINE_TERMINATOR '#'
#define VESTALINE_TIMEOUT     1000   // ms
#define VESTALINE_BUF_LEN     64
#define VESTALINE_EXPECTED_VERSION "10:01"

VestalFocuser::VestalFocuser()
{
    setVersion(0, 6);
    FI::SetCapability(FOCUSER_CAN_ABORT |
                      FOCUSER_CAN_ABS_MOVE |
                      FOCUSER_CAN_REL_MOVE |
                      FOCUSER_HAS_BACKLASH);
}

const char *VestalFocuser::getDefaultName()
{
    return "VestalFocuser";
}

bool VestalFocuser::initProperties()
{
    INDI::Focuser::initProperties();

    // Temperature
    IUFillNumber(&TemperatureN[0], "TEMPERATURE", "Celsius", "%6.1f", -100, 100, 0, 0);
    IUFillNumberVector(&TemperatureNP, TemperatureN, 1, getDeviceName(),
                       "FOCUS_TEMPERATURE", "Temperature",
                       MAIN_CONTROL_TAB, IP_RO, 0, IPS_IDLE);

    // Max Position
    IUFillNumber(&MaxPositionN[0], "MAX_POSITION", "Steps", "%d", 100, 100000, 1000, 16384);
    IUFillNumberVector(&MaxPositionNP, MaxPositionN, 1, getDeviceName(),
                       "MAX_POSITION", "Max Steps",
                       OPTIONS_TAB, IP_RW, 0, IPS_IDLE);

    // Reverse Rotation
    IUFillSwitch(&ReverseS[REVERSE_OFF], "REVERSE_OFF", "Normal", ISS_ON);
    IUFillSwitch(&ReverseS[REVERSE_ON],  "REVERSE_ON",  "Reverse", ISS_OFF);
    IUFillSwitchVector(&ReverseSP, ReverseS, 2, getDeviceName(),
                       "REVERSE", "Direction",
                       OPTIONS_TAB, IP_RW, ISR_1OFMANY, 0, IPS_OK);

    // Focuser limits
    FocusAbsPosNP[0].setMin(0);
    FocusAbsPosNP[0].setMax(100000);
    FocusAbsPosNP[0].setStep(100);

    FocusRelPosNP[0].setMin(0);
    FocusRelPosNP[0].setMax(10000);
    FocusRelPosNP[0].setStep(10);

    FocusSpeedNP[0].setMin(1);
    FocusSpeedNP[0].setMax(10);
    FocusSpeedNP[0].setStep(1);

    addDebugControl();

    return true;
}

bool VestalFocuser::Handshake()
{
    return readVersion();
}

bool VestalFocuser::updateProperties()
{
    if (isConnected())
    {
        readPosition();
    }

    INDI::Focuser::updateProperties();

    if (isConnected())
    {
        if (readTemperature())
            defineProperty(&TemperatureNP);

        defineProperty(&MaxPositionNP);
        defineProperty(&ReverseSP);

        // Apply saved config
        if (MaxPositionN[0].value > 0)
        {
            FocusAbsPosNP[0].setMax(MaxPositionN[0].value);
            FocusRelPosNP[0].setMax(MaxPositionN[0].value / 10);
        }

        LOG_INFO("VestalFocuser ready.");
    }
    else
    {
        deleteProperty(TemperatureNP.name);
        deleteProperty(MaxPositionNP.name);
        deleteProperty(ReverseSP.name);
    }

    return true;
}

void VestalFocuser::TimerHit()
{
    if (!isConnected())
        return;

    // Read position every cycle
    bool positionChanged = false;
    double prevPosition = FocusAbsPosNP[0].getValue();
    readPosition();
    if (prevPosition != FocusAbsPosNP[0].getValue())
        positionChanged = true;

    // Check if move completed
    if ((FocusAbsPosNP.getState() == IPS_BUSY ||
         FocusRelPosNP.getState() == IPS_BUSY) &&
        !isMoving())
    {
        FocusAbsPosNP.setState(IPS_OK);
        FocusRelPosNP.setState(IPS_OK);
        FocusAbsPosNP.apply();
        FocusRelPosNP.apply();
    }
    else if (positionChanged)
    {
        FocusAbsPosNP.apply();
    }

    // Read temperature periodically
    if (TemperatureNP.s == IPS_OK &&
        ++m_TemperatureCounter >= TEMPERATURE_POLL_FREQ)
    {
        m_TemperatureCounter = 0;
        if (readTemperature())
            IDSetNumber(&TemperatureNP, nullptr);
    }

    SetTimer(getCurrentPollingPeriod());
}

bool VestalFocuser::isMoving()
{
    char res[VESTALINE_BUF_LEN] = {0};
    if (!sendVestalineCommand(":GI#", res, sizeof(res)))
        return false;

    return res[0] == '0' && res[1] == '1';
}

bool VestalFocuser::readPosition()
{
    char res[VESTALINE_BUF_LEN] = {0};
    if (!sendVestalineCommand(":GP#", res, sizeof(res)))
        return false;

    int pos = 0;
    if (sscanf(res, "%x", &pos) != 1)
    {
        LOGF_ERROR("Invalid position response: %s", res);
        return false;
    }

    if (m_ReverseRotation)
        pos = -pos;

    FocusAbsPosNP[0].setValue(pos);
    return true;
}

bool VestalFocuser::readTemperature()
{
    char res[VESTALINE_BUF_LEN] = {0};
    if (!sendVestalineCommand(":GT#", res, sizeof(res)))
        return false;

    short raw = 0;
    if (sscanf(res, "%hx", &raw) != 1)
    {
        LOGF_ERROR("Invalid temperature response: %s", res);
        return false;
    }

    double tempC = raw / 2.0;

    // Sentry value from firmware when NTC disconnected
    if (tempC < -50)
        return false;

    TemperatureN[0].value = tempC;
    TemperatureNP.s = IPS_OK;
    return true;
}

bool VestalFocuser::readVersion()
{
    char res[VESTALINE_BUF_LEN] = {0};
    if (!sendVestalineCommand(":GV#", res, sizeof(res)))
        return false;

    return strncmp(res, VESTALINE_EXPECTED_VERSION, 5) == 0;
}

IPState VestalFocuser::MoveAbsFocuser(uint32_t targetTicks)
{
    if (targetTicks > (uint32_t)m_MaxPosition)
    {
        LOGF_ERROR("Target %u exceeds MaxStep %d", targetTicks, m_MaxPosition);
        return IPS_ALERT;
    }

    int target = static_cast<int>(targetTicks);
    if (m_ReverseRotation)
        target = -target;

    char cmd[VESTALINE_BUF_LEN];
    snprintf(cmd, sizeof(cmd), ":SN%08X#", target);

    // Send target position
    if (!sendVestalineCommand(cmd, nullptr, 0))
        return IPS_ALERT;

    // Small delay between commands
    std::this_thread::sleep_for(std::chrono::milliseconds(50));

    // Start move
    if (!sendVestalineCommand(":FG#", nullptr, 0))
        return IPS_ALERT;

    FocusAbsPosNP.setState(IPS_BUSY);
    FocusRelPosNP.setState(IPS_BUSY);

    return IPS_BUSY;
}

IPState VestalFocuser::MoveRelFocuser(FocusDirection dir, uint32_t ticks)
{
    int direction = (dir == FOCUS_INWARD) ? -1 : 1;
    int delta = static_cast<int>(ticks) * direction;

    if (m_ReverseRotation)
        delta = -delta;

    uint32_t target = static_cast<uint32_t>(FocusAbsPosNP[0].getValue() + delta);

    if (delta < 0 && static_cast<int>(FocusAbsPosNP[0].getValue()) < -delta)
        target = 0;

    if (target > static_cast<uint32_t>(m_MaxPosition))
        target = static_cast<uint32_t>(m_MaxPosition);

    return MoveAbsFocuser(target);
}

bool VestalFocuser::AbortFocuser()
{
    return sendVestalineCommand(":FQ#", nullptr, 0);
}

bool VestalFocuser::SyncFocuser(uint32_t ticks)
{
    FocusAbsPosNP[0].setValue(ticks);
    FocusAbsPosNP.setState(IPS_OK);
    FocusAbsPosNP.apply();
    return true;
}

bool VestalFocuser::sendVestalineCommand(const char *cmd, char *res, int resLen)
{
    int nbytes_written = 0, nbytes_read = 0, rc = -1;

    tcflush(PortFD, TCIOFLUSH);

    LOGF_DEBUG("CMD <%s>", cmd);

    // Send command
    rc = tty_write_string(PortFD, cmd, &nbytes_written);
    if (rc != TTY_OK)
    {
        char errstr[MAXRBUF] = {0};
        tty_error_msg(rc, errstr, MAXRBUF);
        LOGF_ERROR("Serial write error: %s.", errstr);
        return false;
    }

    // Check if response expected
    if (res == nullptr || resLen <= 0)
        return true;

    // Read response terminated by VESTALINE_TERMINATOR
    char buf[VESTALINE_BUF_LEN] = {0};
    rc = tty_nread_section(PortFD, buf, sizeof(buf) - 1,
                           VESTALINE_TERMINATOR, VESTALINE_TIMEOUT, &nbytes_read);
    if (rc != TTY_OK)
    {
        char errstr[MAXRBUF] = {0};
        tty_error_msg(rc, errstr, MAXRBUF);
        LOGF_ERROR("Serial read error: %s.", errstr);
        return false;
    }

    // Strip trailing terminator and copy to output
    if (nbytes_read > 0 && buf[nbytes_read - 1] == VESTALINE_TERMINATOR)
        nbytes_read--;

    if (nbytes_read < static_cast<int>(sizeof(*res) * resLen) ? nbytes_read : resLen - 1)
        nbytes_read = nbytes_read < resLen - 1 ? nbytes_read : resLen - 1;

    memcpy(res, buf, nbytes_read);
    res[nbytes_read] = '\0';

    LOGF_DEBUG("RES <%s>", res);

    tcflush(PortFD, TCIOFLUSH);
    return true;
}

bool VestalFocuser::ISNewSwitch(const char *dev, const char *name,
                                 ISState *states, char *names[], int n)
{
    if (dev && !strcmp(dev, getDeviceName()))
    {
        // Reverse rotation
        if (!strcmp(name, ReverseSP.name))
        {
            IUUpdateSwitch(&ReverseSP, states, names, n);
            ReverseSP.s = IPS_OK;
            m_ReverseRotation = (ReverseS[REVERSE_ON].s == ISS_ON);
            IDSetSwitch(&ReverseSP, nullptr);
            return true;
        }
    }

    return INDI::Focuser::ISNewSwitch(dev, name, states, names, n);
}

bool VestalFocuser::ISNewNumber(const char *dev, const char *name,
                                 double values[], char *names[], int n)
{
    if (dev && !strcmp(dev, getDeviceName()))
    {
        // Max Position
        if (!strcmp(name, MaxPositionNP.name))
        {
            IUUpdateNumber(&MaxPositionNP, values, names, n);
            m_MaxPosition = static_cast<int>(MaxPositionN[0].value);
            FocusAbsPosNP[0].setMax(m_MaxPosition);
            FocusRelPosNP[0].setMax(m_MaxPosition / 10);
            MaxPositionNP.s = IPS_OK;
            IDSetNumber(&MaxPositionNP, nullptr);
            LOGF_INFO("MaxPosition set to %d", m_MaxPosition);
            return true;
        }
    }

    return INDI::Focuser::ISNewNumber(dev, name, values, names, n);
}

bool VestalFocuser::saveConfigItems(FILE *fp)
{
    INDI::Focuser::saveConfigItems(fp);

    IUSaveConfigNumber(fp, &MaxPositionNP);
    IUSaveConfigSwitch(fp, &ReverseSP);

    return true;
}
