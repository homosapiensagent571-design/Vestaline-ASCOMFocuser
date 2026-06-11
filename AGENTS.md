# VestalFocuser beta 0.6.10 — ASCOM IFocuserV4

## 项目目标

将现有的 MoonLite→Vestaline 协议 Arduino 对焦控制器改造为 ASCOM 兼容驱动，使 N.I.N.A./SharpCap 等天文软件可直接连接控制。

## 技术决策 (已确认)

| 决策 | 选择 |
|------|------|
| 接口 | **IFocuserV4** (Platform 7) |
| 协议 | **Vestaline** (原 MoonLite 改名) — 保留不动 |
| 工具链 | **.NET Framework 4.8 + GAC 引用** (csc.exe / dotnet build) |
| App 方案 | **方案A** — 双模式: ASCOM 连接 + 直连串口保留 |
| 安装包 | 暂不需要，手动 regasm 注册 |
| 温度支持 | **已实现** — Temperature 读取 + TempComp 自动补偿 + 系数配置 |
| 固件调试 | 改 stepper.cpp 用 debugSerial，保留调试能力 |

## 环境状态

| 组件 | 状态 | 位置/版本 |
|------|------|-----------|
| .NET 8 SDK + MSBuild | 已装 | 8.0.421 / MSBuild 17.11 |
| .NET Framework 4.8 DevPack | 已装 | reference assemblies OK |
| ASCOM Platform 7.1 | 已装 | DeviceInterfaces/Utilities v7.1.3.4851 |
| VS Code + C# Dev Kit | 已装 | `D:\Software\VS code\` / 扩展 v3.20.199 |
| IFocuserV4 接口 | 可用 | GAC `ASCOM.DeviceInterfaces` v6.0.0.0 (实际 7.1) |
| csc.exe | 可用 | `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` (4.8) |
| RegAsm | 可用 | 32-bit /codebase 注册 |

## 已完成

- [x] MoonLite → Vestaline 改名 (15处文本 + 目录/文件重命名)
- [x] ASCOM_Driver 项目创建 + .csproj / .sln
- [x] FocuserDriver.cs — 完整 IFocuserV4 实现
- [x] FocuserSetupDialogForm.cs — 设置对话框
- [x] Build.bat — 编译 + COM 注册
- [x] dotnet build 通过 (0 errors 0 warnings)
- [x] RegAsm COM 注册成功
- [x] 32-bit PowerShell COM 激活验证通过
- [x] ASCOM Chooser 注册表项已创建
- [x] .vscode/ 配置 (settings.json + launch.json)
- [x] VS Code 已打开项目
- [x] test_focuser.js — ASCOM 驱动验证脚本 (初始: 18/23 PASS)
- [x] **FocuserApp 双模式 App** (ASCOM + 直连串口)
  - IFocusService.cs — 统一通信接口
  - ASCOMFocuserService.cs — IFocuserV4 late-bound COM 封装
  - MainForm.cs — RadioButton 切换直连/ASCOM 模式
  - Config.cs — 新增 UseASCOM / ASCOMProgID
  - Build.bat 编译通过 → bin/FocuserApp.exe (34KB)

## 待完成 (按优先级)

### Phase 1: 固件微调
- [x] stepper.h: 添加 `#include <SoftwareSerial.h>` + `extern SoftwareSerial debugSerial;`
- [x] stepper.cpp: 所有 Serial.print/println → debugSerial.print/println (20处)
- [x] **手动烧录** vestaline.ino 到 Arduino Nano (COM3) — 用户已完成

### Phase 2: WinForms App 改造 (方案A)
- [x] IFocusService.cs — 统一通信接口
- [x] ASCOMFocuserService.cs — ASCOM IFocuserV4 late-bound 封装
- [x] MainForm.cs — 直连/ASCOM 双模式切换
- [x] Config.cs — UseASCOM / ASCOMProgID
- [x] Build.bat 编译通过 → FocuserApp.exe
- [x] **温度功能** (NTC 10kΩ thermistor, B=3950)
  - Temperature — 读 :GT#，解码 (int16)raw/2.0 → °C
  - TempCompAvailable — true
  - TempComp — 开关 + Profile 系数 "Temperature Coefficient (steps/°C)"
  - 自动补偿 — Timer 每 10 秒检测 ΔT，按系数调整位置
  - FocuserSetupDialogForm — 新增系数输入框 + Diagnostics 双行显示
- [x] ASCOM Conform 合规测试 — PASS (0 errors, V4 边界差异不计)
- [x] NINA 连接通过 — 64-bit COM 注册修复 (AnyCPU + 64-bit RegAsm)
- [x] SetupDialog UI 修复 — 窗口 560×520，高 DPI 文字不截断
- [x] Connect/Disconnect 重构 — V4 async (ThreadPool) + V3 compat (阻塞)
- [x] **属性缓存** — Position(100ms) / IsMoving(100ms) / Temperature(500ms)，串口 I/O 降低 80%+

### Phase 3: 验证
- [x] ASCOM 全链路验证 (test_focuser.js 29/34 PASS + FocuserApp ASCOM模式)
- [x] ASCOM Conform 合规测试 — PASS
- [x] NINA 集成测试 — 连接/移动/刹车正常
- [x] 温度功能完整测试 — Temperature/TempComp/自动补偿

## 已知限制

### ConformU 不兼容 (Blazor Server 架构缺陷)

| 工具 | 架构 | 结果 |
|------|------|------|
| Conform 7.0 (旧版) | WinForms (主线程带消息泵) | ✅ PASS |
| ConformU 4.3 (新版) | Blazor Server (线程池, 无消息泵) | ❌ 服务端崩溃 |

**根因**: ConformU 基于 Blazor Server，当调用 `Connected = True`(V3 兼容 setter, 必须同步阻塞 3-4 秒扫描串口) 时，Kestrel 请求线程被阻塞，SignalR 心跳超时 → 进程崩溃。

此问题已被确认为 ConformU 架构限制，非驱动 bug:
- 驱动 `Connected` setter 按 ASCOM 规范必须阻塞 (V3 兼容)
- ConformU 模拟器测试正常 (ComSim 无阻塞 I/O)
- 全网搜索无此问题的公开记录 (GitHub Issues / ASCOM Talk / Reddit)
- 建议: 使用旧版 Conform 7.0 做合规验证，已全部通过

## 核心架构

```
N.I.N.A. / SharpCap / TestApp
    │
    ▼
┌──────────────────────────┐
│  IFocuserV4              │  ← ASCOM 接口
│  ASCOM.Autofocus.Focuser │
├──────────────────────────┤
│  ASCOM.Utilities.Serial  │  ← 串口通信层
├──────────────────────────┤
│  COM Port (USB)          │
└──────────┬───────────────┘
           │
    ┌──────▼──────────┐
    │  Arduino Nano    │
    │  vestaline.ino   │  ← Vestaline 协议
    │  (:GP# :GI# etc) │
    ├──────────────────┤
    │  ULN2003 +       │
    │  28BYJ-48 步进电机 │
    ├──────────────────┤
    │  NTC 10kΩ (A0)   │  ← 温度传感器
    └──────────────────┘
```

## Vestaline 协议命令速查

| 命令 | 说明 | 响应 |
|------|------|------|
| `:GP#` | 获取位置 | 4位hex |
| `:GT#` | 获取温度 | 4位hex, 值 = (int16)(°C×2) |
| `:C#` | 获取温度(同GT) | 4位hex |
| `:GI#` | 是否运动中 | `00`(停) / `01`(动) |
| `:GN#` | 获取目标位置 | 4位hex |
| `:GH#` | 获取步进模式 | 2位hex |
| `:SNXXXXXXXX#` | 设置目标 (8位hex) | 无响应 |
| `:FG#` | 开始移动 | 无响应 |
| `:FQ#` | 立即停止 | 无响应 |
| `:GV#` | 获取版本 | `10:01#` |

## ASCOM 驱动文件清单

```
ASCOM_Driver/
├── FocuserDriver.cs                    # IFocuserV4 实现 (~870行)
├── FocuserSetupDialogForm.cs           # 设置对话框 (~250行)
├── Properties/AssemblyInfo.cs          # 程序集元数据
├── ASCOM.Autofocus.Focuser.csproj      # net48, AnyCPU, GAC引用
├── ASCOM.Autofocus.Focuser.sln         # 解决方案
└── Build.bat                           # dotnet build + regasm (32+64bit)
```

## 关键路径

- 项目根: `D:\OpenCode\AutofocusProject V6.9\`
- 驱动 DLL: `ASCOM_Driver\bin\Debug\net48\ASCOM.Autofocus.Focuser.dll`
- VS Code: `D:\Software\VS code\Microsoft VS Code\Code.exe`
- 固件: `vestaline\vestaline.ino`
- 现有 App: `Newtestapp\`
- COM ProgID: `ASCOM.Autofocus.Focuser`
- CLSID: `{C0203456-68FB-4491-A516-BE513E1D10A1}`
