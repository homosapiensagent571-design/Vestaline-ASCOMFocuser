#!/bin/bash
# ============================================================
# VestalFocuser INDI Driver — Linux 一键部署脚本
# 适用于 Debian/Ubuntu 系统
# ============================================================
set -e

DRIVER_NAME="vestal_focuser"
INSTALL_DIR="/opt/$DRIVER_NAME"
INDI_SRC="$HOME/Projects/indi"
LOG_FILE="/tmp/$DRIVER_NAME-deploy.log"

# 颜色
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err()  { echo -e "${RED}[✗]${NC} $1"; exit 1; }

# ——————————————————————————————————————————
# 1. 安装编译依赖
# ——————————————————————————————————————————
echo "=== Step 1: Install dependencies ===" | tee $LOG_FILE
sudo apt update -y >> $LOG_FILE 2>&1
sudo apt install -y \
    build-essential \
    cmake \
    git \
    libindi-dev \
    libnova-dev \
    >> $LOG_FILE 2>&1 || err "Failed to install dependencies"
log "Dependencies installed"

# ——————————————————————————————————————————
# 2. 克隆 INDI 源码（如果还没有）
# ——————————————————————————————————————————
echo "=== Step 2: Prepare INDI source tree ==="
if [ ! -d "$INDI_SRC" ]; then
    git clone --depth 1 https://github.com/indilib/indi.git "$INDI_SRC" >> $LOG_FILE 2>&1 \
        || err "Failed to clone INDI"
    log "Cloned INDI to $INDI_SRC"
else
    log "INDI source already exists at $INDI_SRC"
fi

# ——————————————————————————————————————————
# 3. 放置驱动源码到 INDI 树
# ——————————————————————————————————————————
echo "=== Step 3: Install driver source ==="
DRV_DIR="$INDI_SRC/drivers/$DRIVER_NAME"
mkdir -p "$DRV_DIR"

# === 需要把以下三个文件放在与本脚本同目录 ===
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cp "$SCRIPT_DIR/vestal_focuser.h"   "$DRV_DIR/" 2>/dev/null || warn "vestal_focuser.h not found in script dir"
cp "$SCRIPT_DIR/vestal_focuser.cpp" "$DRV_DIR/" 2>/dev/null || warn "vestal_focuser.cpp not found in script dir"
cp "$SCRIPT_DIR/CMakeLists.txt"     "$DRV_DIR/" 2>/dev/null || warn "CMakeLists.txt not found in script dir"

log "Driver source placed in $DRV_DIR"

# ——————————————————————————————————————————
# 4. 编译驱动
# ——————————————————————————————————————————
echo "=== Step 4: Build driver ==="
BUILD_DIR="$INDI_SRC/build"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

cmake .. -DCMAKE_BUILD_TYPE=Release -G "Unix Makefiles" >> $LOG_FILE 2>&1 \
    || err "CMake configure failed"
make -j$(nproc) indi_vestal_focuser >> $LOG_FILE 2>&1 \
    || err "Build failed"
log "Driver built successfully"

# ——————————————————————————————————————————
# 5. 安装到系统
# ——————————————————————————————————————————
echo "=== Step 5: Install driver ==="
sudo cp "$BUILD_DIR/drivers/$DRIVER_NAME/indi_vestal_focuser" /usr/bin/ >> $LOG_FILE 2>&1 \
    || err "Failed to install binary"
log "Installed to /usr/bin/indi_vestal_focuser"

# ——————————————————————————————————————————
# 6. 创建 udev 规则（自动识别 Arduino）
# ——————————————————————————————————————————
echo "=== Step 6: Setup udev rule ==="
if [ ! -f /etc/udev/rules.d/99-vestal-focuser.rules ]; then
    sudo tee /etc/udev/rules.d/99-vestal-focuser.rules > /dev/null << 'UDEV'
# VestalFocuser Arduino Nano
SUBSYSTEM=="tty", ATTRS{idVendor}=="0403", ATTRS{idProduct}=="6001", SYMLINK+="vestal_focuser", MODE="0666"
SUBSYSTEM=="tty", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="7523", SYMLINK+="vestal_focuser", MODE="0666"
UDEV
    sudo udevadm control --reload-rules >> $LOG_FILE 2>&1
    sudo udevadm trigger >> $LOG_FILE 2>&1
    log "udev rule created — Arduino will appear as /dev/vestal_focuser"
else
    log "udev rule already exists"
fi

# ——————————————————————————————————————————
# 7. 创建 systemd 服务（开机自启 INDI server）
# ——————————————————————————————————————————
echo "=== Step 7: Setup systemd service ==="
if [ ! -f /etc/systemd/system/indi-vestal.service ]; then
    sudo tee /etc/systemd/system/indi-vestal.service > /dev/null << UNIT
[Unit]
Description=INDI VestalFocuser Server
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/indiserver -v indi_vestal_focuser
Restart=on-failure
RestartSec=10
User=$USER

[Install]
WantedBy=multi-user.target
UNIT
    sudo systemctl daemon-reload >> $LOG_FILE 2>&1
    sudo systemctl enable indi-vestal 2>/dev/null || warn "Could not enable service (non-root?)"
    log "systemd service created (disabled by default, enable with: sudo systemctl enable --now indi-vestal)"
else
    log "systemd service already exists"
fi

echo ""
echo "============================================================"
echo "  Deployment Complete"
echo "============================================================"
echo ""
echo "  Run command:"
echo "    indiserver -v indi_vestal_focuser"
echo ""
echo "  Or with explicit port:"
echo "    indiserver -p 7624 -v indi_vestal_focuser"
echo ""
echo "  Then in KStars/Ekos:"
echo "    Profile → Focuser → VestalFocuser → Port: /dev/vestal_focuser"
echo ""
echo "  Log file: $LOG_FILE"
