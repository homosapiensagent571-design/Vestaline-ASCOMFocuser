using System;
using System.IO;
using System.Web.Script.Serialization;

public class Config
{
    public string PortName { get; set; }
    public int BaudRate { get; set; }
    public int SmallStep { get; set; }
    public int MidStep { get; set; }
    public int BigStep { get; set; }
    public int MaxTravel { get; set; }
    public bool AutoConnect { get; set; }
    public int FocalPlane { get; set; }

    public Config()
    {
        PortName = "COM3";
        BaudRate = 9600;
        SmallStep = 10;
        MidStep = 50;
        BigStep = 200;
        MaxTravel = 16384;
        AutoConnect = false;
        FocalPlane = -1;
    }

    public static Config Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var js = new JavaScriptSerializer();
                string json = File.ReadAllText(path);
                return js.Deserialize<Config>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Config load error: " + ex.Message);
        }
        return new Config();
    }

    public void Save(string path)
    {
        Save(path, this);
    }

    public static void Save(string path, Config config)
    {
        try
        {
            var js = new JavaScriptSerializer();
            string json = js.Serialize(config);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Config save error: " + ex.Message);
        }
    }
}
