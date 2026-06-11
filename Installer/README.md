# VestalFocuser beta 0.6.10

ASCOM IFocuserV4 电调焦驱动，专为 Arduino Nano + ULN2003 + 28BYJ-48 步进电机设计。

---

## 1. 系统要求

| 组件 | 最低版本 |
|------|---------|
| Windows | 10 1809+ 或 11 |
| .NET Framework | 4.8 |
| ASCOM Platform | 7.1+ |
| Arduino | Nano (COM 口连接) |

---

## 2. 安装

1. 下载 `VestalFocuser-0.6.10-Setup.exe`
2. **右键 → 以管理员身份运行**
3. 点击 Install，等待完成
4. 安装程序自动完成：
   - 复制驱动到 `C:\Program Files\VestalFocuser\`
   - 注册 32 位 + 64 位 COM 组件
   - 写入 ASCOM Profile
   - 创建开始菜单快捷方式

**升级：** 直接运行新版安装包即可覆盖，自动反注册旧版本后安装新版本。安装目录与旧版相同。

---

## 3. 连接 Arduino

1. 用 USB 线连接 Arduino Nano
2. 打开 Arduino IDE，上传 `vestaline.ino` 固件
3. 记住串口号（如 COM3）

---

## 4. NINA 连接设置

1. 启动 N.I.N.A.
2. 进入 **Equipment → Focuser**
3. 搜索或在下拉列表中找到 **VestalFocuser beta 0.6.10**
4. 点击连接图标

**首次使用前需要配置 COM 口：**
1. 点击设置按钮（齿轮图标）
2. 如果 Arduino 已连接，默认开启 **Auto-detect COM port**
3. 或者关闭自动检测，手动选择 COM 口
4. 点击 OK 保存

---

## 5. 设置对话框说明

| 设置项 | 说明 | 默认值 |
|--------|------|--------|
| Auto-detect COM port | 自动扫描并识别 Arduino | 开启 |
| Port | 手动指定 COM 口 | — |
| Scan | 刷新 COM 口列表 | — |
| Max Steps | 最大行程（步数） | 16384 |
| Reverse rotation | 反转旋转方向 | 关闭 |
| Steps per °C | 温度补偿系数（步/°C） | 0（禁用） |
| Enable trace logging | 写入调试日志到 Documents\ASCOM\Logs | — |

---

## 6. NINA 基本操作

### 手动对焦
- 使用 NINA 电调焦面板的 **+/- 按钮**或**滑块**移动
- 也可在 Position 输入框直接输入目标步数，点 **Move**

### 自动对焦
- NINA 的自动对焦功能**无需驱动额外配置**
- 在 NINA Advanced Sequencer 中添加 Focus 指令即可
- 对焦精度取决于步进电机和光学系统，驱动侧不干预

### 归零
- 在 NINA Position 输入框输入 `0`，点 Move
- 或创建 Sequence 指令设置 Focuser Position = 0

---

## 7. 温度补偿

### 前提
- Arduino A0 引脚需连接 NTC 10kΩ (B=3950) 热敏电阻
- 配置 5V → 10kΩ 固定电阻 → A0 → NTC → GND 分压电路

### 启用补偿

1. 在 NINA 设置面板中，勾选 **Temperature compensation**
2. 在驱动设置对话框中，填入 **Steps per °C** 系数

### 测定系数（标定方法）

1. 白天或室温稳定时，将望远镜对准远处物体
2. 记录当前温度 T₀ 和对焦位置 P₀
3. 让设备升温或降温（如用吹风机加热镜筒）
4. 重新对焦，记录 T₁ 和 P₁
5. 计算系数：**(P₁ - P₀) / (T₁ - T₀)** 步/°C

例如：25°C 时位置 8000 步，30°C 时重新对焦到 8100 步

> 系数 = (8100 - 8000) / (30 - 25) = 100 / 5 = **20 steps/°C**

### 自动补偿行为

- 温度补偿开启后，驱动每 10 秒检测一次温度变化
- 当温差 ≥ 0.2°C 时，自动移动对应步数
- 马达正在移动时不触发调整
- NTC 未连接时（温度 < -50°C）自动跳过

---

## 8. 卸载

1. 开始菜单 → VestalFocuser → **Uninstall VestalFocuser**
2. 或通过 Windows 设置 → 应用 → 卸载
3. 卸载程序自动：
   - 反注册 COM 组件
   - 清理 ASCOM Profile
   - 删除所有文件

---

## 9. 故障排除

| 现象 | 可能原因 | 解决方法 |
|------|---------|---------|
| NINA 找不到驱动 | 64 位 COM 未注册 | 以管理员身份重装 |
| 连接失败 | COM 口错误 / 波特率不匹配 | 检查串口号，确认 9600 bps |
| 马达不动 | ULN2003 接线错误 / 电源不足 | 检查接线，尝试外接 5V 电源 |
| 温度显示 60+°C | NTC 未连接 | 正常现象，插上 NTC 即可 |
| 马达丢步 | 负载过重 / 速度过快 | 降低最大步数或增加减速比 |
| 自动对焦不准 | 28BYJ-48 精度限制 | 考虑升级步进电机 |
| 设置对话框文字截断 | 高 DPI 缩放 | 已适配 100%-150% DPI |

---

## 10. 文件位置

| 内容 | 路径 |
|------|------|
| 驱动 DLL | `C:\Program Files\VestalFocuser\` |
| 控制面板 | 开始菜单 → VestalFocuser → VestalFocuser Control |
| 调试日志 | `Documents\ASCOM\Logs <日期>\` |
| ASCOM Profile | `HKCU\SOFTWARE\ASCOM\Profile\Focuser\` |

---

## 硬件连接示意

```
Arduino Nano:
  D2  → IN1 (ULN2003)
  D3  → IN2 (ULN2003)
  D4  → IN3 (ULN2003)
  D5  → IN4 (ULN2003)
  A0  → NTC 热敏电阻 (10kΩ, B=3950)
          ┌─ 5V ─── 10kΩ ─┬─ A0 ─── NTC ─── GND
  5V  → ULN2003 VCC
  GND → ULN2003 GND
```

---

*VestalFocuser beta 0.6.10 | ASCOM IFocuserV4 | Platform 7.1+*
