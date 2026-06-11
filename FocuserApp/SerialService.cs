using System;
using System.IO.Ports;

public enum LogType { Send, Receive, Error, Info }

public class LogEventArgs : EventArgs
{
    public string Message { get; set; }
    public LogType Type { get; set; }
}

public class SerialService : IFocusService
{
    private SerialPort _port;
    public event EventHandler<LogEventArgs> OnLog;

    public bool IsConnected
    {
        get { return _port != null && _port.IsOpen; }
    }

    public string PortName
    {
        get { return _port != null ? _port.PortName : ""; }
    }

    public void Connect(string portName, int baudRate)
    {
        try
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _port.ReadTimeout = 2000;
            _port.WriteTimeout = 1000;
            _port.DtrEnable = false;
            _port.RtsEnable = false;
            _port.Open();
            _port.DiscardInBuffer();
            Log("串口 " + portName + " 已连接, 波特率 " + baudRate, LogType.Info);
        }
        catch (Exception ex)
        {
            Log("连接失败: " + ex.Message, LogType.Error);
            if (_port != null) { _port.Dispose(); _port = null; }
            throw;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_port != null)
            {
                if (_port.IsOpen) _port.Close();
                _port.Dispose();
                Log("串口已断开", LogType.Info);
            }
        }
        catch { }
        _port = null;
    }

    /// <summary>
    /// Send a Vestaline command. If expectResponse=true, reads response terminated by #.
    /// Query commands (:GP#, :GT#, :GI#) expect a response.
    /// Action commands (:SN#, :FG#, :FQ#) do not return a response.
    /// </summary>
    public string SendCommand(string command, bool expectResponse = true)
    {
        if (!IsConnected)
        {
            Log("错误: 串口未连接", LogType.Error);
            return null;
        }

        try
        {
            Log(">>> " + command, LogType.Send);
            _port.Write(command);

            if (!expectResponse)
                return "#"; // success placeholder for action commands

            _port.DiscardInBuffer();  // clear stale data before reading response
            string response = _port.ReadTo("#");
            if (!string.IsNullOrEmpty(response))
            {
                response = response.Trim();
                Log("<<< " + response, LogType.Receive);
            }
            return response;
        }
        catch (TimeoutException)
        {
            if (expectResponse)
                Log("!!! 超时: " + command, LogType.Error);
            return null;
        }
        catch (InvalidOperationException)
        {
            Log("!!! 串口已断开", LogType.Error);
            return null;
        }
        catch (Exception ex)
        {
            Log("!!! 错误: " + ex.Message, LogType.Error);
            return null;
        }
    }

    private void DiscardInBuffer()
    {
        if (!IsConnected) return;
        try
        {
            _port.DiscardInBuffer();
        }
        catch { }
    }

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    private void Log(string message, LogType type)
    {
        if (OnLog != null)
        {
            OnLog(this, new LogEventArgs { Message = message, Type = type });
        }
    }
}
