# INDI VestalFocuser — Linux Agent 部署文档

## 目标

将 VestalFocuser 编译为 INDI 驱动，使其可在 KStars/Ekos 等 INDI 客户端中控制 Arduino + 28BYJ-48 电调焦。

## 前置条件

| 组件 | 版本要求 |
|------|---------|
| Linux | Debian 12 / Ubuntu 22.04+ |
| INDI | 2.0+ (libindi-dev) |
| CMake | 3.13+ |
| GCC | 8.0+ |
| 串口 | /dev/ttyUSB0 或 /dev/ttyACM0 (Arduino Nano) |

## 文件清单

```
INDI V6.11/
├── vestal_focuser.h        # 类定义 (INDI::Focuser 继承)
├── vestal_focuser.cpp      # 完整实现 (~300行)
├── CMakeLists.txt           # 构建脚本
├── deploy.sh                # 一键部署
├── AGENTS.md                # 本文档
└── README.md                # 用户文档
```

## 核心决策

| 决策 | 选择 |
|------|------|
| 基类 | `INDI::Focuser` (Platform 7 equivalent) |
| 串口库 | `indicom.h` (tty_read/write, 跨平台) |
| 协议 | Vestaline (:GP#, :GI#, :GT#, :FGi#, etc.) |
| 编译方式 | 放入 INDI 源码树 `drivers/vestal_focuser/` |
| 温度 | 只读，补偿由客户端 (Ekos) 完成 |
| 配置 | XML 持久化 (~/.indi/VestalFocuser_config.xml) |

## 构建方式

### 方式A: 放入 INDI 树编译 (推荐)

```bash
# 把驱动源码放到 INDI 源码的 drivers/ 下
cp vestal_focuser.h vestal_focuser.cpp CMakeLists.txt \
   ~/Projects/indi/drivers/vestal_focuser/

# 编译
cd ~/Projects/indi/build
cmake .. -DCMAKE_BUILD_TYPE=Release
make indi_vestal_focuser -j$(nproc)

# 安装
sudo cp ~/Projects/indi/build/drivers/vestal_focuser/indi_vestal_focuser /usr/bin/
```

### 方式B: 独立编译

```bash
mkdir build && cd build
cmake .. -DCMAKE_PREFIX_PATH=/usr
make -j$(nproc)
sudo cp indi_vestal_focuser /usr/bin/
```

### 方式C: 一键部署

```bash
chmod +x deploy.sh && sudo ./deploy.sh
```

## 运行

```bash
# 启动
indiserver -v indi_vestal_focuser

# 指定端口
indiserver -p 7624 -v indi_vestal_focuser

# 连接后 KStars/Ekos:
# Profile Editor → Focuser → VestalFocuser
# Connection 标签 → Port: /dev/ttyUSB0 (或 /dev/vestal_focuser)
```

## 调试

```bash
# 详细日志
indiserver -v -v indi_vestal_focuser 2>&1 | tee debug.log

# 串口测试
screen /dev/ttyUSB0 9600
# 手动发送 :GV# 应返回 10:01#

# 查看 INDI 属性
indi_getprop | grep -i vestal
```

## INDI 属性与 Vestaline 命令映射

| INDI Property | Vestaline CMD | 方向 |
|---------------|---------------|------|
| FOCUS_ABSOLUTE_POSITION | `:SNXXXXXXXX#` + `:FG#` | 写 |
| FOCUS_TEMPERATURE | `:GT#` | 读 |
| isMoving (内部) | `:GI#` | 读 |
| readPosition (内部) | `:GP#` | 读 |
| Handshake | `:GV#` | 读写 |
| Abort | `:FQ#` | 写 |

## 与 ASCOM 驱动的差异

| 方面 | ASCOM (Windows) | INDI (Linux) |
|------|----------------|-------------|
| 语言 | C# / .NET 4.8 | C++ / INDI lib |
| 架构 | COM in-proc DLL | 独立进程 (indiserver fork) |
| 通信 | 客户端直接调 COM | TCP → indiserver → pipe |
| 配置 | Profile (注册表) | XML 文件 (~/.indi/*) |
| 温度补偿 | 驱动内置 Timer | 客户端 (Ekos) 负责 |
| 步进模式 | 无 (28BYJ-48 固定) | 跳过 (无硬件支持) |
| 安装 | exe + RegAsm | deb / tar.gz + cp |

## 已知限制

- INDI Focuser 接口无原生温度补偿 — 由客户端实现
- 28BYJ-48 无编码器 — 位置依赖 Arduino 步数计数，丢步会累积偏差
- Windows 上需通过 WSL2 或 MSYS2/MinGW 编译
- 串口速率固定 9600 (Vestaline 协议要求)

## 关键路径

- INDI 驱动二进制: `/usr/bin/indi_vestal_focuser`
- 配置文件: `~/.indi/VestalFocuser_config.xml`
- 日志: `indiserver` 的 stdout/stderr
- 串口设备: `/dev/vestal_focuser` (udev 别名) 或 `/dev/ttyUSB0`
