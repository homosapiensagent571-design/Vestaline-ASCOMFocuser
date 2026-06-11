using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ASCOM.Autofocus
{
    [Guid("c0203456-68fb-4491-a516-be513e1d10a1")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Focuser : IFocuserV4
    {
        internal static string driverID = "ASCOM.Autofocus.Focuser";
        private static readonly string deviceName = "VestalFocuser beta 0.6.10";

        // Profile keys
        internal static string autoDetectComPortProfileName = "Auto-Detect COM Port";
        internal static string autoDetectComPortDefault = "true";
        internal static string comPortProfileName = "COM Port";
        internal static string comPortDefault = "COM1";
        internal static string lastComPortProfileName = "Last COM Port";
        internal static string lastComPortDefault = string.Empty;
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "true";
        internal static string maxPositionProfileName = "Maximum Position";
        internal static string maxPositionDefault = "16384";
        internal static string reverseRotationProfileName = "Reverse Rotation";
        internal static string reverseRotationDefault = "false";

        internal static string tempCoefficientProfileName = "Temperature Coefficient (steps/°C)";
        internal static string tempCoefficientDefault = "0";

        // Configuration values
        internal static bool autoDetectComPort = Convert.ToBoolean(autoDetectComPortDefault);
        internal static string comPortOverride = comPortDefault;
        internal static int maxPosition = Convert.ToInt32(maxPositionDefault);
        internal static bool reverseRotation = Convert.ToBoolean(reverseRotationDefault);
        internal static double tempCoefficient = Convert.ToDouble(tempCoefficientDefault);

        internal TraceLogger tl;

        private bool _connected;
        private bool _tempComp;
        private double _refTemperature;

        // Property value cache (reduces serial I/O for rapid polling by ConformU/NINA)
        private readonly object _cacheLock = new object();
        private int _cachedPosition;
        private bool _cachedIsMoving;
        private double _cachedTemperature;
        private DateTime _positionCacheTime = DateTime.MinValue;
        private DateTime _isMovingCacheTime = DateTime.MinValue;
        private DateTime _temperatureCacheTime = DateTime.MinValue;

        // Temperature compensation timer
        private System.Threading.Timer _tempCompTimer;

        // Async connect state
        private bool _connecting;
        private readonly object lockObject = new object();
        private Serial objSerial;

        // Vestaline protocol constants
        private const char VESTALINE_TERMINATOR = '#';
        private const string VESTALINE_CMD_PREFIX = ":";

        private const string CMD_GET_POSITION = ":GP#";
        private const string CMD_GET_TEMP = ":GT#";
        private const string CMD_GET_ISMOVING = ":GI#";
        private const string CMD_GET_VERSION = ":GV#";
        private const string CMD_HALT = ":FQ#";
        private const string CMD_SET_TARGET_PREFIX = ":SN";
        private const string CMD_START_MOVE = ":FG#";

        private const string EXPECTED_VERSION = "10:01";

        public Focuser()
        {
            tl = new TraceLogger("", "VestalineAutofocus");
            ReadProfile();
            LogMessage("Focuser", "Starting initialization");
            _connected = false;
            _connecting = false;
            LogMessage("Focuser", "Completed initialization");
        }

        // ============================================================
        // IFocuserV4 — Connection (new in V4)
        // ============================================================

        public void Connect()
        {
            LogMessage("Connect", "Called (async)");
            if (_connected || _connecting) return;
            Thread connectThread = new Thread(DoConnectWork) { IsBackground = true };
            connectThread.Start();
        }

        public void Disconnect()
        {
            LogMessage("Disconnect", "Called (async)");
            if (!_connected && !_connecting) return;
            Thread disconnectThread = new Thread(DoDisconnectWork) { IsBackground = true };
            disconnectThread.Start();
        }

        private void DoConnectWork()
        {
            lock (lockObject)
            {
                if (_connected || _connecting) return;
                _connecting = true;
            }
            try
            {
                LogMessage("Connect", "Connecting to device...");
                if (objSerial != null)
                {
                    try { objSerial.Connected = false; } catch { }
                    try { objSerial.Dispose(); } catch { }
                    objSerial = null;
                }
                using (Profile driverProfile = new Profile() { DeviceType = "Focuser" })
                {
                    Serial serial = null;
                    var comPorts = new List<string>(System.IO.Ports.SerialPort.GetPortNames());
                    if (autoDetectComPort)
                    {
                        string lastComPort = driverProfile.GetValue(driverID, lastComPortProfileName, string.Empty, string.Empty);
                        if (!string.IsNullOrEmpty(lastComPort))
                        {
                            int i = comPorts.IndexOf(lastComPort);
                            if (i >= 0)
                            {
                                comPorts.RemoveAt(i);
                                comPorts.Insert(0, lastComPort);
                            }
                        }
                        foreach (string comPortName in comPorts)
                        {
                            serial = ConnectToDevice(comPortName);
                            if (serial != null) break;
                        }
                    }
                    else if (comPorts.Contains(comPortOverride))
                    {
                        serial = ConnectToDevice(comPortOverride);
                    }
                    else
                    {
                        LogMessage("Connect", "Invalid COM port: {0}", comPortOverride);
                    }
                    if (serial != null)
                    {
                        objSerial = serial;
                        driverProfile.WriteValue(driverID, lastComPortProfileName, serial.PortName);
                        LogMessage("Connect", "Connected to port {0}", serial.PortName);
                        _connected = true;
                        lock (_cacheLock)
                        {
                            _positionCacheTime = DateTime.MinValue;
                            _isMovingCacheTime = DateTime.MinValue;
                            _temperatureCacheTime = DateTime.MinValue;
                        }
                    }
                    else
                    {
                        LogMessage("Connect", "Failed to connect to device");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("Connect", "Connection error: {0}", ex.Message);
            }
            finally
            {
                _connecting = false;
            }
        }

        private void DoDisconnectWork()
        {
            lock (lockObject)
            {
                if (!_connected && !_connecting) return;
                _connecting = true;
            }
            try
            {
                StopTempComp();
                _connected = false;
                LogMessage("Disconnect", "Disconnecting");
                lock (_cacheLock)
                {
                    _positionCacheTime = DateTime.MinValue;
                    _isMovingCacheTime = DateTime.MinValue;
                    _temperatureCacheTime = DateTime.MinValue;
                }
                if (objSerial != null)
                {
                    try { objSerial.Connected = false; } catch { }
                    try { objSerial.Dispose(); } catch { }
                    objSerial = null;
                }
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                LogMessage("Disconnect", "Disconnect error: {0}", ex.Message);
            }
            finally
            {
                _connecting = false;
            }
        }

        public bool Connecting
        {
            get
            {
                LogMessage("Connecting Get", _connecting.ToString());
                return _connecting;
            }
        }

        // ============================================================
        // IFocuserV4 — DeviceState (new in V4)
        // ============================================================

        public IStateValueCollection DeviceState
        {
            get
            {
                var states = new StateValueCollection();
                try
                {
                    if (_connected)
                    {
                        states.Add(new StateValue("IsMoving", IsMoving));
                        states.Add(new StateValue("Position", Position));
                    }
                }
                catch
                {
                    // If any property throws, omit it
                }
                states.Add(new StateValue("TimeStamp", DateTime.UtcNow.ToString("o")));
                return states;
            }
        }

        // ============================================================
        // IFocuserV4 — Properties (standard + V2/V3)
        // ============================================================

        public bool Absolute
        {
            get
            {
                LogMessage("Absolute Get", true.ToString());
                return true;
            }
        }

        public bool IsMoving
        {
            get
            {
                lock (_cacheLock)
                {
                    // 100ms cache when stopped, 200ms when moving (faster refresh for stop detection)
                    double maxAge = _cachedIsMoving ? 200 : 100;
                    if ((DateTime.Now - _isMovingCacheTime).TotalMilliseconds < maxAge)
                        return _cachedIsMoving;
                }
                string response = SendVestalineCommand("IsMoving", CMD_GET_ISMOVING, true);
                if (response != "00" && response != "01")
                {
                    LogMessage("IsMoving", "Invalid response from device: " + response);
                    throw new DriverException("Invalid response from device: " + response);
                }
                bool moving = response == "01";
                lock (_cacheLock)
                {
                    _cachedIsMoving = moving;
                    _isMovingCacheTime = DateTime.Now;
                }
                return moving;
            }
        }

        public bool Link
        {
            get { return Connected; }
            set { Connected = value; }
        }

        public int MaxIncrement
        {
            get
            {
                LogMessage("MaxIncrement Get", maxPosition.ToString());
                return maxPosition;
            }
        }

        public int MaxStep
        {
            get
            {
                LogMessage("MaxStep Get", maxPosition.ToString());
                return maxPosition;
            }
        }

        public void Move(int Position)
        {
            if (Position < 0 || Position > maxPosition)
            {
                throw new InvalidValueException("Position", Position.ToString(), "0", maxPosition.ToString());
            }

            if (reverseRotation) Position = -Position;

            string targetCmd = string.Format(":SN{0:X8}#", Position);

            lock (lockObject)
            {
                CheckConnected("Move");
                LogMessage("Move", "Setting target: {0}", Position);

                // :SN and :FG return no response — send without waiting
                objSerial.Transmit(targetCmd);
                Thread.Sleep(50);
                objSerial.Transmit(CMD_START_MOVE);
            }

            LogMessage("Move", "Command sent: {0} + {1}", targetCmd, CMD_START_MOVE);
            lock (_cacheLock)
            {
                _positionCacheTime = DateTime.MinValue;
                _isMovingCacheTime = DateTime.MinValue;
            }
        }

        public int Position
        {
            get
            {
                lock (_cacheLock)
                {
                    if ((DateTime.Now - _positionCacheTime).TotalMilliseconds < 100)
                        return _cachedPosition;
                }
                string response = SendVestalineCommand("Position", CMD_GET_POSITION, true);
                int value;
                try
                {
                    value = int.Parse(response, NumberStyles.HexNumber);
                }
                catch (FormatException)
                {
                    LogMessage("Position", "Invalid position value: " + response);
                    throw new DriverException("Invalid position value from device: " + response);
                }

                if (reverseRotation) value = -value;
                lock (_cacheLock)
                {
                    _cachedPosition = value;
                    _positionCacheTime = DateTime.Now;
                }
                return value;
            }
        }

        public double StepSize
        {
            get
            {
                LogMessage("StepSize Get", "Not implemented");
                throw new PropertyNotImplementedException("StepSize", true);
            }
        }

        public bool TempComp
        {
            get
            {
                LogMessage("TempComp Get", _tempComp.ToString());
                return _tempComp;
            }
            set
            {
                LogMessage("TempComp Set", value.ToString());
                if (value == _tempComp) return;
                _tempComp = value;
                if (_tempComp)
                    StartTempComp();
                else
                    StopTempComp();
            }
        }

        private void StartTempComp()
        {
            if (tempCoefficient == 0)
            {
                LogMessage("TempComp", "Coefficient is 0, compensation disabled");
                return;
            }
            if (_tempCompTimer != null) return;
            try { _refTemperature = Temperature; }
            catch { _refTemperature = 20; }
            _tempCompTimer = new System.Threading.Timer(TempCompCallback, null, 10000, 10000);
            LogMessage("TempComp", "Started: ref={0:F1}°C, coeff={1:F1} steps/°C", _refTemperature, tempCoefficient);
        }

        private void StopTempComp()
        {
            if (_tempCompTimer == null) return;
            _tempCompTimer.Dispose();
            _tempCompTimer = null;
            LogMessage("TempComp", "Stopped");
        }

        private void TempCompCallback(object state)
        {
            try
            {
                if (!_connected || !_tempComp || Math.Abs(tempCoefficient) < 0.001) return;
                double currentTemp;
                try { currentTemp = Temperature; } catch { return; }
                if (currentTemp < -50) return;
                double delta = currentTemp - _refTemperature;
                if (Math.Abs(delta) < 0.2) return;
                int offset = (int)Math.Round(delta * tempCoefficient);
                if (offset == 0) return;
                if (!_connected || IsMoving) return;
                int currentPos = Position;
                int newPos = currentPos + offset;
                if (newPos < 0) newPos = 0;
                if (newPos > maxPosition) newPos = maxPosition;
                if (newPos == currentPos) return;
                Move(newPos);
                _refTemperature = currentTemp;
                LogMessage("TempComp", "Adjusted: ΔT={0:F1}°C, {1} steps → pos {2}", delta, offset, newPos);
            }
            catch (Exception ex)
            {
                LogMessage("TempComp", "Timer error: {0}", ex.Message);
            }
        }

        public bool TempCompAvailable
        {
            get
            {
                LogMessage("TempCompAvailable Get", "True");
                return true;
            }
        }

        public double Temperature
        {
            get
            {
                lock (_cacheLock)
                {
                    if ((DateTime.Now - _temperatureCacheTime).TotalMilliseconds < 500)
                        return _cachedTemperature;
                }
                CheckConnected("Temperature");
                string response = SendVestalineCommand("Temperature", CMD_GET_TEMP, true);
                try
                {
                    int raw = int.Parse(response, NumberStyles.HexNumber);
                    short signedValue = (short)raw;
                    double tempC = signedValue / 2.0;
                    LogMessage("Temperature Get", "{0:F1}°C", tempC);
                    lock (_cacheLock)
                    {
                        _cachedTemperature = tempC;
                        _temperatureCacheTime = DateTime.Now;
                    }
                    return tempC;
                }
                catch (FormatException)
                {
                    LogMessage("Temperature", "Invalid temperature value from device: " + response);
                    throw new DriverException("Invalid temperature value from device: " + response);
                }
            }
        }

        public void Halt()
        {
            lock (lockObject)
            {
                CheckConnected("Halt");
                LogMessage("Halt", "Sending halt command");
                objSerial.Transmit(CMD_HALT);
            }
            lock (_cacheLock)
            {
                _isMovingCacheTime = DateTime.MinValue;
                _positionCacheTime = DateTime.MinValue;
            }
        }

        // ============================================================
        // Common Properties
        // ============================================================

        public bool Connected
        {
            get
            {
                LogMessage("Connected Get", _connected.ToString());
                return _connected;
            }
            set
            {
                LogMessage("Connected Set", value.ToString());
                if (value == _connected) return;

                if (value)
                {
                    DoConnectWork(); // V3 compat: blocking
                }
                else
                {
                    DoDisconnectWork(); // V3 compat: blocking
                }
            }
        }

        public string Description
        {
            get
            {
                LogMessage("Description Get", deviceName);
                return deviceName;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = deviceName + " ASCOM Driver Version " +
                    string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                LogMessage("InterfaceVersion Get", "4");
                return 4;
            }
        }

        public string Name
        {
            get
            {
                LogMessage("Name Get", deviceName);
                return deviceName;
            }
        }

        // ============================================================
        // SetupDialog
        // ============================================================

        public void SetupDialog()
        {
            if (_connected)
            {
                MessageBox.Show("Already connected, just press OK");
            }

            using (FocuserSetupDialogForm F = new FocuserSetupDialogForm(this))
            {
                var result = F.ShowDialog();
                if (result == DialogResult.OK)
                {
                    WriteProfile();
                }
            }
        }

        // ============================================================
        // Actions (none for now)
        // ============================================================

        public ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Empty list");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            LogMessage("Action", "Action {0} not implemented", actionName);
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            throw new MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            throw new MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            throw new MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
        }

        // ============================================================
        // COM Registration
        // ============================================================

        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new Profile())
            {
                P.DeviceType = "Focuser";
                if (bRegister)
                {
                    P.Register(driverID, deviceName);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        // ============================================================
        // Profile (read/write settings)
        // ============================================================

        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                autoDetectComPort = Convert.ToBoolean(driverProfile.GetValue(driverID, autoDetectComPortProfileName, string.Empty, autoDetectComPortDefault));
                comPortOverride = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
                maxPosition = Convert.ToInt32(driverProfile.GetValue(driverID, maxPositionProfileName, string.Empty, maxPositionDefault));
                reverseRotation = Convert.ToBoolean(driverProfile.GetValue(driverID, reverseRotationProfileName, string.Empty, reverseRotationDefault));
                tempCoefficient = Convert.ToDouble(driverProfile.GetValue(driverID, tempCoefficientProfileName, string.Empty, tempCoefficientDefault));
            }
        }

        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, autoDetectComPortProfileName, autoDetectComPort.ToString());
                if (comPortOverride != null)
                {
                    driverProfile.WriteValue(driverID, comPortProfileName, comPortOverride.ToString());
                }
                driverProfile.WriteValue(driverID, maxPositionProfileName, maxPosition.ToString());
                driverProfile.WriteValue(driverID, reverseRotationProfileName, reverseRotation.ToString());
                driverProfile.WriteValue(driverID, tempCoefficientProfileName, tempCoefficient.ToString(CultureInfo.InvariantCulture));
            }
        }

        // ============================================================
        // Vestaline Serial Communication
        // ============================================================

        private Serial ConnectToDevice(string comPortName)
        {
            if (!System.IO.Ports.SerialPort.GetPortNames().Contains(comPortName))
            {
                throw new InvalidValueException("Invalid COM port", comPortName,
                    string.Join(", ", System.IO.Ports.SerialPort.GetPortNames()));
            }

            Serial serial;
            LogMessage("ConnectToDevice", "Connecting to port {0}", comPortName);

            try
            {
                serial = new Serial
                {
                    Speed = SerialSpeed.ps9600,
                    PortName = comPortName,
                    Connected = true,
                    DTREnable = false,
                    RTSEnable = false,
                    ReceiveTimeout = 1
                };
                LogMessage("ConnectToDevice", "Port {0} opened OK", comPortName);
            }
            catch (Exception ex)
            {
                LogMessage("ConnectToDevice", "Port {0} open FAILED: {1}", comPortName, ex.Message);
                return null;
            }

            Thread.Sleep(500);
            serial.ClearBuffers();

            for (int retries = 3; retries >= 0; retries--)
            {
                string response = string.Empty;

                lock (lockObject)
                {
                    try
                    {
                        serial.Transmit(CMD_GET_VERSION);
                        response = serial.ReceiveTerminated(VESTALINE_TERMINATOR.ToString()).Trim();
                        // ASCOM.Utilities.Serial.ReceiveTerminated INCLUDES the terminator in response
                        if (response.EndsWith(VESTALINE_TERMINATOR.ToString()))
                            response = response.Substring(0, response.Length - 1);
                        LogMessage("ConnectToDevice", "Port {0} attempt {1}: response='{2}'", comPortName, retries, response);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("ConnectToDevice", "Port {0} attempt {1}: error={2}", comPortName, retries, ex.Message);
                    }
                }

                if (response == EXPECTED_VERSION)
                {
                    serial.ReceiveTimeout = 5;
                    LogMessage("ConnectToDevice", "Port {0} CONFIRMED as Vestaline!", comPortName);
                    return serial;
                }
            }

            LogMessage("ConnectToDevice", "Port {0} NOT Vestaline, closing", comPortName);

            serial.Connected = false;
            serial.Dispose();
            return null;
        }

        private string SendVestalineCommand(string identifier, string command, bool expectResponse)
        {
            CheckConnected(identifier);

            string response;
            lock (lockObject)
            {
                LogMessage(identifier, "Sending Vestaline command: " + command);
                objSerial.Transmit(command);

                if (!expectResponse)
                {
                    LogMessage(identifier, "No response expected");
                    return string.Empty;
                }

                LogMessage(identifier, "Waiting for response...");
                try
                {
                    response = objSerial.ReceiveTerminated(VESTALINE_TERMINATOR.ToString()).Trim();
                    if (response.EndsWith(VESTALINE_TERMINATOR.ToString()))
                        response = response.Substring(0, response.Length - 1);
                }
                catch (Exception e)
                {
                    LogMessage(identifier, "Exception: " + e.Message);
                    throw;
                }
            }

            LogMessage(identifier, "Response: " + response);
            return response;
        }

        private string StripTerminator(string s)
        {
            if (s.EndsWith(VESTALINE_TERMINATOR.ToString()))
                return s.Substring(0, s.Length - 1);
            return s;
        }

        private string SendRaw(string command)
        {
            objSerial.Transmit(command);
            try
            {
                string resp = objSerial.ReceiveTerminated(VESTALINE_TERMINATOR.ToString()).Trim();
                if (resp.EndsWith(VESTALINE_TERMINATOR.ToString()))
                    resp = resp.Substring(0, resp.Length - 1);
                return resp;
            }
            catch (TimeoutException)
            {
                return string.Empty;
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void CheckConnected(string message)
        {
            if (!_connected)
            {
                throw new NotConnectedException(message);
            }
        }

        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }
    }
}
