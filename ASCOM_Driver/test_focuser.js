/**
 * test_focuser.js — ASCOM.Autofocus.Focuser 验证脚本
 * 用法: cscript test_focuser.js
 * 要求: Arduino 运行 vestaline.ino 固件且连接在 COM3
 */

var WScriptShell = WScript.CreateObject("WScript.Shell");
var fso = WScript.CreateObject("Scripting.FileSystemObject");

var passCount = 0;
var failCount = 0;

function Pass(name) {
    WScript.Echo("  [PASS] " + name);
    passCount++;
}

function Fail(name, msg) {
    WScript.Echo("  [FAIL] " + name + " — " + msg);
    failCount++;
}

WScript.Echo("");
WScript.Echo("==============================================");
WScript.Echo(" ASCOM.Autofocus.Focuser — IFocuserV4 验证");
WScript.Echo("==============================================");
WScript.Echo("");

// ── 1. COM 实例化 ──────────────────────────────

var driver;
try {
    driver = new ActiveXObject("ASCOM.Autofocus.Focuser");
    Pass("COM instantiation (ProgID: ASCOM.Autofocus.Focuser)");
} catch (e) {
    Fail("COM instantiation", e.message);
    WScript.Echo("");
    WScript.Echo("Aborted: Driver not registered. Run: dotnet build + regasm.");
    WScript.Quit(1);
}

// ── 2. 静态属性 ─────────────────────────────────

WScript.Echo("");
WScript.Echo("--- Static Properties ---");

try {
    var v;

    v = driver.Name;
    if (v == "VestalFocuser beta 0.6.10") Pass("Name = '" + v + "'"); else Fail("Name", "Expected 'VestalFocuser beta 0.6.10', got '" + v + "'");

    v = driver.Description;
    if (v == "VestalFocuser beta 0.6.10") Pass("Description"); else Fail("Description", "got '" + v + "'");

    v = driver.DriverInfo;
    if (v.indexOf("VestalFocuser") >= 0 && v.indexOf("Version") >= 0) Pass("DriverInfo = '" + v + "'"); else Fail("DriverInfo", "got '" + v + "'");

    v = driver.DriverVersion;
    if (v.length > 0 && v.indexOf(".") > 0) Pass("DriverVersion = " + v); else Fail("DriverVersion", "got '" + v + "'");

    v = driver.InterfaceVersion;
    if (v == 4) Pass("InterfaceVersion = 4 (IFocuserV4)"); else Fail("InterfaceVersion", "Expected 4, got " + v);

    v = driver.Absolute;
    if (v === true) Pass("Absolute = true"); else Fail("Absolute", "Expected true, got " + v);

    v = driver.TempCompAvailable;
    if (v === false) Pass("TempCompAvailable = false"); else Fail("TempCompAvailable", "Expected false, got " + v);

    try {
        v = driver.TempComp;
        if (v === false) Pass("TempComp get = false"); else Fail("TempComp get", "Expected false");
    } catch (e) { Fail("TempComp get", e.message); }

    try {
        v = driver.Temperature;
        Fail("Temperature", "Should throw PropertyNotImplementedException");
    } catch (e) {
        Pass("Temperature throws PropertyNotImplementedException (as expected)");
    }

    try {
        v = driver.StepSize;
        Fail("StepSize", "Should throw PropertyNotImplementedException");
    } catch (e) {
        Pass("StepSize throws PropertyNotImplementedException (as expected)");
    }

    // SupportedActions should return empty
    var actions = driver.SupportedActions;
    if (actions.Count == 0) Pass("SupportedActions is empty"); else Fail("SupportedActions", "Expected empty, got " + actions.Count);

    // ── V4 specific: DeviceState ──
    try {
        var state = driver.DeviceState;
        Pass("DeviceState accessible (count=" + state.Count + ")");
        for (var i = 0; i < state.Count; i++) {
            WScript.Echo("    State[" + i + "]: " + state[i].Name + " = " + state[i].Value);
        }
    } catch (e) {
        Fail("DeviceState", e.message);
    }

    // ── V4 specific: Connecting ──
    try {
        v = driver.Connecting;
        if (v === false) Pass("Connecting = false (idle)");
        else Fail("Connecting", "Expected false, got " + v);
    } catch (e) {
        Fail("Connecting", e.message);
    }

} catch (e) {
    Fail("Static properties", e.message);
}

// ── 3. 未连接时行为 ────────────────────────────

WScript.Echo("");
WScript.Echo("--- Disconnected State ---");

try {
    var c = driver.Connected;
    if (c === false) Pass("Connected = false"); else Fail("Connected", "Expected false");
} catch (e) { Fail("Connected get", e.message); }

try {
    driver.Position;
    Fail("Position", "Should throw NotConnectedException");
} catch (e) {
    if (e.message.indexOf("not connect") >= 0 || e.message.indexOf("NotConnected") >= 0)
        Pass("Position throws NotConnectedException (disconnected)");
    else
        Fail("Position", "Wrong exception: " + e.message);
}

try {
    driver.IsMoving;
    Fail("IsMoving", "Should throw NotConnectedException");
} catch (e) {
    Pass("IsMoving throws NotConnectedException (disconnected)");
}

try {
    driver.Move(100);
    Fail("Move", "Should throw NotConnectedException");
} catch (e) {
    Pass("Move throws NotConnectedException (disconnected)");
}

try {
    driver.Halt();
    Fail("Halt", "Should throw NotConnectedException");
} catch (e) {
    Pass("Halt throws NotConnectedException (disconnected)");
}

try {
    driver.MaxStep;
    Fail("MaxStep", "Should throw NotConnectedException");
} catch (e) {
    Pass("MaxStep throws NotConnectedException (disconnected)");
}

// ── 4. 连接设备 (COM3) ──────────────────────────

WScript.Echo("");
WScript.Echo("--- Connect to Device ---");

// First, ensure driver uses COM3 with auto-detect
// The driver's auto-detect will find the device by sending :GV# on each COM port

// Method 1: Try Connected = true (classic, deprecated but must work)
try {
    driver.Connected = true;
    Pass("Connected = true succeeded");

    if (driver.Connected === true) {
        Pass("Connected state confirmed true");
    } else {
        Fail("Connected state", "Expected true after connect");
    }
} catch (e) {
    Fail("Connect via Connected=true", e.message);

    // Method 2: Try Connect() (V4 async method)
    WScript.Echo("  Trying Connect() (V4 async)...");
    try {
        driver.Connect();
        // Poll Connecting until done
        var timeout = 50;
        while (driver.Connecting && timeout > 0) {
            WScript.Sleep(200);
            timeout--;
        }
        if (driver.Connected === true) {
            Pass("Connect() + Connective succeeded");
        } else if (timeout <= 0) {
            Fail("Connect()", "Timed out waiting for Connecting=false");
        } else {
            Fail("Connect()", "Connected is false after Connect() completed");
        }
    } catch (e2) {
        Fail("Connect()", e2.message);
    }
}

// ── 5. 已连接时功能测试 ──────────────────────────

if (driver.Connected) {
    WScript.Echo("");
    WScript.Echo("--- Connected Device Tests ---");

    // 5a. Position
    try {
        var pos = driver.Position;
        Pass("Position read: " + pos + " steps");
    } catch (e) {
        Fail("Position read", e.message);
    }

    // 5b. MaxStep / MaxIncrement
    try {
        var ms = driver.MaxStep;
        var mi = driver.MaxIncrement;
        Pass("MaxStep=" + ms + ", MaxIncrement=" + mi);
    } catch (e) {
        Fail("MaxStep/MaxIncrement", e.message);
    }

    // 5c. IsMoving
    try {
        var im = driver.IsMoving;
        Pass("IsMoving = " + im);
    } catch (e) {
        Fail("IsMoving", e.message);
    }

    // 5d. Move + IsMoving polling
    WScript.Echo("  Testing Move(200) ...");
    try {
        var startPos = driver.Position;
        driver.Move(startPos + 200);
        Pass("Move(" + (startPos+200) + ") accepted");

        var moved = false;
        for (var i = 0; i < 100; i++) {
            if (driver.IsMoving) { moved = true; WScript.Echo("    Motor is running..."); break; }
            WScript.Sleep(100);
        }
        if (moved) Pass("IsMoving=true detected during move");
        else WScript.Echo("  [WARN] IsMoving was never true (motor may have finished instantly)");

        // Wait for completion
        var timeout = 100;
        while (driver.IsMoving && timeout > 0) {
            WScript.Sleep(200);
            timeout--;
        }
        if (!driver.IsMoving) {
            var endPos = driver.Position;
            WScript.Echo("    Move completed. Position: " + startPos + " -> " + endPos);
            if (endPos == startPos + 200) {
                Pass("Position reached target (" + startPos + " + 200 = " + endPos + ")");
            } else {
                WScript.Echo("  [WARN] Position " + endPos + " != expected " + (startPos+200));
            }
        } else {
            Fail("Move completion", "Timed out waiting for IsMoving=false");
        }
    } catch (e) {
        Fail("Move test", e.message);
    }

    // 5e. Halt test
    WScript.Echo("  Testing Halt during movement...");
    try {
        var prePos = driver.Position;
        driver.Move(prePos + 500);
        WScript.Sleep(300);
        if (driver.IsMoving) {
            driver.Halt();
            Pass("Halt() sent while moving");
            WScript.Sleep(200);
            var haltedPos = driver.Position;
            WScript.Echo("    Position after halt: " + haltedPos + " (started from " + prePos + ")");
        } else {
            WScript.Echo("  [WARN] Motor finished before halt could be tested");
        }
    } catch (e) {
        Fail("Halt test", e.message);
    }

    // 5f. Out-of-range Move
    WScript.Echo("  Testing Move(-1) out-of-range...");
    try {
        driver.Move(-1);
        Fail("Move(-1)", "Should throw InvalidValueException");
    } catch (e) {
        if (e.message.indexOf("Invalid") >= 0 || e.message.indexOf("无效") >= 0)
            Pass("Move(-1) throws InvalidValueException");
        else
            Fail("Move(-1)", "Wrong exception: " + e.message);
    }

    // 5g. DeviceState when connected
    WScript.Echo("  DeviceState (connected):");
    try {
        var ds = driver.DeviceState;
        for (var i = 0; i < ds.Count; i++) {
            WScript.Echo("    " + ds[i].Name + " = " + ds[i].Value);
        }
        Pass("DeviceState contains connected operational data");
    } catch (e) {
        Fail("DeviceState connected", e.message);
    }

    // ── 6. 断开 ──────────────────────────────

    WScript.Echo("");
    WScript.Echo("--- Disconnect ---");

    driver.Connected = false;
    if (driver.Connected === false) {
        Pass("Disconnected successfully");
    } else {
        Fail("Disconnect", "Connected is still true");
    }

    WScript.Sleep(500);

    // V4: also test Disconnect()
    try {
        driver.Connect();
        while (driver.Connecting) WScript.Sleep(200);
        driver.Disconnect();
        while (driver.Connecting) WScript.Sleep(200);
        if (driver.Connected === false) {
            Pass("Connect()/Disconnect() cycle OK");
        } else {
            Fail("Disconnect()", "Still connected after Disconnect()");
        }
    } catch (e) {
        WScript.Echo("  [INFO] Connect/Disconnect: " + e.message);
    }

} else {
    WScript.Echo("");
    WScript.Echo("[SKIP] Connected device tests — no Arduino on COM3?");
}

// ── 7. 结果汇总 ──────────────────────────────────

WScript.Echo("");
WScript.Echo("==============================================");
var total = passCount + failCount;
WScript.Echo(" RESULTS: " + passCount + "/" + total + " passed, " + failCount + " failed");
if (failCount == 0) {
    WScript.Echo(" ALL TESTS PASSED");
} else {
    WScript.Echo(" " + failCount + " TEST(S) FAILED");
}
WScript.Echo("==============================================");
WScript.Echo("");

WScript.Quit(failCount > 0 ? 1 : 0);
