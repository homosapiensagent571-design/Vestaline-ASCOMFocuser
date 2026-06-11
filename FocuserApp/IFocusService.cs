using System;

public interface IFocusService
{
    bool IsConnected { get; }
    string SendCommand(string command, bool expectResponse);
}
