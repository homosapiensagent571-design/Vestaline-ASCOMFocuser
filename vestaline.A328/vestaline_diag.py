"""
Vestaline 连接诊断工具
用法: python vestaline_diag.py COM3
逐层验证: 端口 → 波特率 → 协议握手 → 功能命令
"""
import sys
import time
import serial

def hex16(val):
    """16-bit int from 4-char hex"""
    return int(val, 16)

def test(port):
    tests = [
        # (波特率, 命令, 期望响应格式)
        (9600,  b':GP#',    r'[0-9A-F]{4}#', '读位置'),
        (9600,  b':GT#',    r'[0-9A-F]{4}#', '读温度'),
        (9600,  b':GI#',    r'[0-9A-F]{2}#', '是否运动中'),
        (9600,  b':GH#',    r'[0-9A-F]{2}#', '步进模式'),
        (9600,  b':GN#',    r'[0-9A-F]{4}#', '目标位置'),
        (9600,  b':GV#',    r'[0-9A-F]{2}:[0-9A-F]{2}#', '固件版本'),
    ]

    ser = None
    passed = 0

    try:
        ser = serial.Serial(port, 9600, timeout=2)
        ser.dtr = False
        ser.rts = False
        time.sleep(1.5)
        ser.reset_input_buffer()

        print(f"[OK] 端口 {port} 已打开")

        for baud, cmd, pattern, desc in tests:
            try:
                if baud != ser.baudrate:
                    ser.baudrate = baud
                    time.sleep(0.3)

                ser.write(cmd)
                time.sleep(0.5)
                resp = ser.readline().decode('ascii', errors='ignore').strip()
                import re
                if re.fullmatch(pattern, resp):
                    print(f"[OK] {cmd.decode():>6} → {resp:<12} ({desc})")
                    passed += 1
                else:
                    print(f"[FAIL] {cmd.decode():>6} → {resp or '(timeout)':<12} ({desc})")
            except Exception as e:
                print(f"[ERR] {cmd.decode():>6} → {e} ({desc})")

    except serial.SerialException as e:
        print(f"[FAIL] 无法打开端口 {port}: {e}")
        print("检查: 端口号是否正确? 是否被其他程序占用?")
        return 1
    except Exception as e:
        print(f"[FAIL] {e}")
        return 1
    finally:
        if ser and ser.is_open:
            ser.close()

    print(f"\n结果: {passed}/{len(tests)} 项通过")
    if passed == 0:
        print("\n排错建议:")
        print("1. 确认固件已烧录 (Arduino IDE 上传成功)")
        print("2. 检查波特率是否匹配 (固件默认 9600)")
        print("3. 用串口助手手动发送 :GP# 测试原始响应")
        print("4. 检查 TX/RX 接线 (Nano TX→USB RX, Nano RX→USB TX)")
        print("5. 检查是否其他程序占用 COM 口")
    return 0 if passed >= 4 else 1

if __name__ == '__main__':
    port = sys.argv[1] if len(sys.argv) > 1 else 'COM3'
    print(f"Vestaline 诊断 — 端口 {port}\n")
    sys.exit(test(port))
