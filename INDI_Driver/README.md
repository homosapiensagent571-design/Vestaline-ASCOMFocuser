# VestalFocuser — INDI Driver

INDI focuser driver for Arduino Nano + ULN2003 + 28BYJ-48 stepper,
using the Vestaline protocol.

## Architecture

```
KStars / Ekos / CCDciel
    │
    ▼ (TCP 7624)
indiserver
    │
    ▼ (stdin/stdout pipe)
indi_vestal_focuser
    │
    ▼ (Serial / COM Port)
Arduino Nano (vestaline.ino)
    │
    ▼
ULN2003 + 28BYJ-48
```

## Vestaline Protocol

| Command | Description | Response |
|---------|-------------|----------|
| `:GP#` | Get Position | 4-digit hex |
| `:GT#` | Get Temperature | 4-digit hex, int16(°C×2) |
| `:GI#` | Is Moving | `00` / `01` |
| `:GV#` | Get Version | `10:01#` |
| `:SNXXXXXXXX#` | Set Target (8 hex) | none |
| `:FG#` | Start Move | none |
| `:FQ#` | Halt | none |

## Build

Inside the INDI source tree:

```bash
# Place in drivers/vestal_focuser/
cd ~/Projects/indi/build
cmake -B build -G Ninja -DCMAKE_INSTALL_PREFIX=/usr -DCMAKE_BUILD_TYPE=Debug ..
ninja
sudo ninja install
```

Standalone build (requires INDI installed):

```bash
mkdir build && cd build
cmake .. -DCMAKE_PREFIX_PATH=/usr
make
```

## Run

```bash
# Start INDI server with the driver
indiserver -v indi_vestal_focuser

# With explicit serial port
indiserver -v indi_vestal_focuser -p 7624
```

Then in KStars/Ekos:
1. Profile Editor → add Focuser → "VestalFocuser"
2. Set serial port (e.g., `/dev/ttyUSB0` on Linux, `COM3` on Windows)
3. Connect

## INDI Properties

| Property | Type | Tab | Description |
|----------|------|-----|-------------|
| CONNECTION | — | Main | Serial port & baud rate |
| FOCUS_ABSOLUTE_POSITION | Number | Main | Current / target position |
| FOCUS_RELATIVE_POSITION | Number | Main | Relative move ticks |
| FOCUS_SPEED | Number | Main | Movement speed (1-10) |
| FOCUS_TEMPERATURE | Number | Main | Temperature (°C) |
| MAX_POSITION | Number | Options | Maximum steps (100-100000) |
| REVERSE | Switch | Options | Normal / Reverse direction |

## Comparison with ASCOM Driver

| Feature | ASCOM (FocuserDriver.cs) | INDI (vestal_focuser.cpp) |
|---------|--------------------------|---------------------------|
| Language | C# / .NET 4.8 | C++ / INDI lib |
| Interface | IFocuserV4 | INDI::Focuser |
| Connection | COM (RegAsm) | TCP (indiserver) |
| Serial | ASCOM.Utilities.Serial | indicom (tty_read/write) |
| Temp comp | Timer-based auto adjust | Client-side only |
| SetupDialog | WinForms GUI | INDI Control Panel |
| Config | ASCOM Profile | ~/.indi/VestalFocuser_config.xml |

## Files

```
INDI V6.11/
├── vestal_focuser.h      # Driver class definition
├── vestal_focuser.cpp    # Implementation (Vestaline protocol)
├── CMakeLists.txt        # Build configuration
└── README.md             # This file
```
