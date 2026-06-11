#pragma once

#include <indifocuser.h>

class VestalFocuser : public INDI::Focuser
{
    public:
        VestalFocuser();
        virtual ~VestalFocuser() = default;

        const char *getDefaultName() override;

        bool initProperties() override;
        bool updateProperties() override;
        bool Handshake() override;
        void TimerHit() override;

        bool ISNewSwitch(const char *dev, const char *name, ISState *states, char *names[], int n) override;
        bool ISNewNumber(const char *dev, const char *name, double values[], char *names[], int n) override;
        bool saveConfigItems(FILE *fp) override;

    protected:
        IPState MoveAbsFocuser(uint32_t targetTicks) override;
        IPState MoveRelFocuser(FocusDirection dir, uint32_t ticks) override;
        bool AbortFocuser() override;
        bool SyncFocuser(uint32_t ticks) override;

    private:
        // Serial communication
        bool sendVestalineCommand(const char *cmd, char *res, int resLen);
        bool readPosition();
        bool readTemperature();
        bool isMoving();
        bool readVersion();

        // Configuration
        int m_MaxPosition {16384};
        bool m_ReverseRotation {false};
        int m_TemperatureCounter {0};
        static constexpr int TEMPERATURE_POLL_FREQ {10};

        // Temperature properties
        INumberVectorProperty TemperatureNP;
        INumber TemperatureN[1];

        // Max position property
        INumberVectorProperty MaxPositionNP;
        INumber MaxPositionN[1];

        // Reverse rotation switch
        ISwitchVectorProperty ReverseSP;
        ISwitch ReverseS[2];
        enum { REVERSE_ON, REVERSE_OFF };
};
