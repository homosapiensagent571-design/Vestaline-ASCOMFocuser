using System;
using System.Runtime.InteropServices;

/// <summary>
/// Wraps the ASCOM IFocuserV4 driver, providing the same interface as SerialService
/// for seamless mode switching in MainForm.
/// </summary>
public class ASCOMFocuserService : IFocusService
{
    private object _focuser;       // IFocuserV4 via late-bound COM
    private Type _focuserType;
    public event EventHandler<LogEventArgs> OnLog;

    public bool IsConnected { get; private set; }
    public string PortName { get; private set; }

    public ASCOMFocuserService()
    {
        IsConnected = false;
        PortName = "ASCOM";
    }

    /// <summary>
    /// Connect via ASCOM Chooser. progId e.g. "ASCOM.Autofocus.Focuser"
    /// </summary>
    public void Connect(string progId)
    {
        Disconnect();

        try
        {
            Log("ASCOM: Creating COM object: " + progId, LogType.Info);
            _focuser = Activator.CreateInstance(Type.GetTypeFromProgID(progId));
            _focuserType = _focuser.GetType();

            // Try Connect() (V4 async) first, fallback to Connected = true
            Log("ASCOM: Connecting...", LogType.Info);

            bool connected = false;
            object[] emptyArgs = new object[0];

            try
            {
                _focuserType.InvokeMember("Connect",
                    System.Reflection.BindingFlags.InvokeMethod, null, _focuser, emptyArgs);

                // Poll Connecting property
                int timeout = 50;
                while (timeout > 0)
                {
                    bool connecting = (bool)_focuserType.InvokeMember("Connecting",
                        System.Reflection.BindingFlags.GetProperty, null, _focuser, emptyArgs);
                    if (!connecting) break;
                    System.Threading.Thread.Sleep(200);
                    timeout--;
                }

                connected = (bool)_focuserType.InvokeMember("Connected",
                    System.Reflection.BindingFlags.GetProperty, null, _focuser, emptyArgs);
            }
            catch
            {
                // Fallback to classic Connected = true
                try
                {
                    _focuserType.InvokeMember("Connected",
                        System.Reflection.BindingFlags.SetProperty, null, _focuser, new object[] { true });
                    connected = true;
                }
                catch
                {
                    // Will throw below
                }
            }

            if (!connected)
                throw new Exception("Failed to connect to ASCOM driver");

            IsConnected = true;

            string name = GetPropertySafe("Name");
            string ver = GetPropertySafe("InterfaceVersion");
            if (name == null) name = "Unknown";
            if (ver == null) ver = "?";
            PortName = name + " (V" + ver + ")";
            Log("ASCOM: Connected — " + name + " InterfaceVersion=" + ver, LogType.Info);
        }
        catch (Exception ex)
        {
            Log("ASCOM: Connection failed — " + ex.Message, LogType.Error);
            Dispose();
            throw;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_focuser != null && IsConnected)
            {
                try
                {
                    object[] empty = new object[0];
                    _focuserType.InvokeMember("Disconnect",
                        System.Reflection.BindingFlags.InvokeMethod, null, _focuser, empty);
                }
                catch { }

                try
                {
                    _focuserType.InvokeMember("Connected",
                        System.Reflection.BindingFlags.SetProperty, null, _focuser, new object[] { false });
                }
                catch { }

                Log("ASCOM: Disconnected", LogType.Info);
            }
        }
        catch { }
        finally
        {
            Dispose();
            IsConnected = false;
        }
    }

    /// <summary>
    /// Send a command (vestaline-compatible). For ASCOM mode, this maps logical
    /// operations rather than raw protocol commands.
    /// 
    /// Mapping:
    ///   ":GP#"       → returns hex string of position
    ///   ":GT#"       → returns hex string of temperature×2 (or "0000")
    ///   ":GI#"       → returns "01" or "00"
    ///   ":SNxxx#"    → remember target
    ///   ":FG#"       → actually invoke Move()
    ///   ":FQ#"       → invoke Halt()
    ///   ":GV#"       → returns "10:01" (compat)
    /// </summary>
    public string SendCommand(string command, bool expectResponse = true)
    {
        if (!IsConnected)
        {
            Log("错误: ASCOM 未连接", LogType.Error);
            return null;
        }

        try
        {
            Log(">>> " + command, LogType.Send);

            if (command == ":GP#")
            {
                int pos = (int)_focuserType.InvokeMember("Position",
                    System.Reflection.BindingFlags.GetProperty, null, _focuser, new object[0]);
                string resp = pos.ToString("X4");
                Log("<<< " + resp, LogType.Receive);
                return resp;
            }
            else if (command == ":GT#")
            {
                // Temperature not implemented, return 0°C
                return "0000";
            }
            else if (command == ":GI#")
            {
                bool moving = (bool)_focuserType.InvokeMember("IsMoving",
                    System.Reflection.BindingFlags.GetProperty, null, _focuser, new object[0]);
                string resp = moving ? "01" : "00";
                Log("<<< " + resp, LogType.Receive);
                return resp;
            }
            else if (command == ":GV#")
            {
                return "10:01";
            }
            else if (command.StartsWith(":SN") && command.EndsWith("#"))
            {
                // Store target hex → int, then call Move()
                string hex = command.Substring(3, command.Length - 4);
                int target = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                Log("ASCOM: Move(" + target + ")", LogType.Send);
                _focuserType.InvokeMember("Move",
                    System.Reflection.BindingFlags.InvokeMethod, null, _focuser, new object[] { target });
                Log("<<< OK", LogType.Receive);
                return "#";
            }
            else if (command == ":FG#")
            {
                // In ASCOM mode, Move already handles movement; :FG is a no-op
                return "#";
            }
            else if (command == ":FQ#")
            {
                Log("ASCOM: Halt", LogType.Send);
                _focuserType.InvokeMember("Halt",
                    System.Reflection.BindingFlags.InvokeMethod, null, _focuser, new object[0]);
                Log("<<< OK", LogType.Receive);
                return "#";
            }
            else
            {
                Log("ASCOM: Unknown command: " + command, LogType.Error);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log("!!! ASCOM Error: " + ex.Message, LogType.Error);
            throw;  // Re-throw so caller sees the error
        }
    }

    public string[] GetAvailableProgIds()
    {
        // Enumerate ASCOM Focuser drivers from registry
        var list = new System.Collections.Generic.List<string>();
        try
        {
            // Standard ASCOM Chooser registry path
            var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\WOW6432Node\ASCOM\Focuser Drivers");
            if (key != null)
            {
                foreach (var name in key.GetSubKeyNames())
                    list.Add(name);
            }
            // Try 64-bit path
            key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\ASCOM\Focuser Drivers");
            if (key != null)
            {
                foreach (var name in key.GetSubKeyNames())
                    if (!list.Contains(name)) list.Add(name);
            }
        }
        catch { }
        return list.ToArray();
    }

    private string GetPropertySafe(string name)
    {
        try
        {
            object result = _focuserType.InvokeMember(name,
                System.Reflection.BindingFlags.GetProperty, null, _focuser, new object[0]);
            if (result == null) return null;
            return result.ToString();
        }
        catch { return null; }
    }

    public void Dispose()
    {
        try
        {
            if (_focuser != null)
            {
                Marshal.ReleaseComObject(_focuser);
            }
        }
        catch { }
        _focuser = null;
        _focuserType = null;
    }

    private void Log(string message, LogType type)
    {
        if (OnLog != null)
            OnLog(this, new LogEventArgs { Message = message, Type = type });
    }
}
