# VestalFocuser beta 0.6.10 (legacy)

直接串口 Vestaline 协议调焦控制软件。跳过 ASCOM/COM 注册，通过 COM 口直连 Arduino 驱动。

## 硬件

- Arduino Nano ATmega328P (CH340)
- ULN2003 + 28BYJ-48 步进电机
- NTC 10kΩ 热敏电阻
- 丝杆螺距 20mm/转, 行程 ~80mm

## 固件参数

| 参数 | 值 | 说明 |
|------|-----|------|
| 波特率 | 9600 | Vestaline 标准 |
| 步进模式 | Half-step | 4096 步/转 |
| 齿轮比 | 32:1 | 实测校准 |
| 最大行程 | 16384 步 | 4转 × 4096 = 80mm |
| 默认速度 | 1667 μs/步 | 600 步/秒 |
| 加速步数 | 256 | 平滑加减速 |
| 协议 | Vestaline `:XXxxxx#` | |

## 编译

需要 .NET Framework 4.x (csc.exe):

```
Build.bat
```

输出: `bin\TestApp.exe`

## 配置文件

`autofocus.config` (JSON):

```json
{
  "PortName": "COM3",
  "BaudRate": 9600,
  "SmallStep": 10,
  "MidStep": 50,
  "BigStep": 200,
  "MaxTravel": 16384,
  "AutoConnect": false,
  "FocalPlane": -1
}
```

## 界面功能

### 左面板 (控制台)
- **位置显示**: 当前步数 (轮询 `:GP#`)
- **温度显示**: NTC 温度 °C (轮询 `:GT#`)
- **状态圆点**: 绿=空闲, 红=运动中 (轮询 `:GI#`)
- **点动按钮**: 大/中/小步长 (< 退 / 进 >), 步长在设置面板修改
- **焦平面**: 保存/返回对焦点位置
- **前往**: 输入目标步数移动
- **HALT**: 紧急停止 (`:FQ#`)
- **归零/设为零位**: 前往 0 位
- **反转方向**: 点动方向取反

### 右面板 (设置)
- **串口号/波特率**: 扫描/连接/断开
- **点动步数**: 小/中/大三档可调
- **最大行程**: 1-16384 步
- **自动连接**: 启动时自动连接上次串口

### 日志 Tab
- 显示收发命令和系统消息
- 支持清空/复制

## Vestaline 命令速查

| 命令 | 说明 | 响应 |
|------|------|------|
| `:GP#` | 获取位置 | 4位hex, 例 `0000` |
| `:GT#` | 获取温度 | 4位hex, 值=温度×2 |
| `:GI#` | 是否运动中 | `00`(停) / `01`(动) |
| `:GN#` | 获取目标位置 | 4位hex |
| `:GH#` | 获取步进模式 | 2位hex, `01`=半步 |
| `:SNxxxxxxxx#` | 设置目标位置 | 无响应 (8位hex) |
| `:SPxxxxxxxx#` | 设置当前位置 | 无响应 |
| `:FG#` | 开始移动 | 无响应 |
| `:FQ#` | 停止 | 无响应 |
| `:GV#` | 获取版本 | `10:01` |

## 文件清单

```
Newtestapp/
├── Program.cs          # 入口
├── MainForm.cs         # 主界面 (WinForms)
├── SerialService.cs    # 串口通信 (Vestaline 协议)
├── Config.cs           # 配置文件读写
├── autofocus.config    # 配置 JSON
├── Build.bat           # 编译脚本
└── bin/
    └── TestApp.exe     # 编译输出
```
