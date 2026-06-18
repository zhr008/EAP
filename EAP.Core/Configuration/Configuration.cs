using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using log4net;

namespace EAP.Core.Configuration;

public enum ProtocolType
{
    OpcUa,
    OpcDa,
    Hsms,
    Modbus
}

public class DeviceConfig
{
    [XmlElement("Id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;

    [XmlIgnore]
    public string FolderName { get; set; } = string.Empty;

    [XmlIgnore]
    public string FilePath { get; set; } = string.Empty;

    [XmlElement("Enabled")]
    public bool Enabled { get; set; } = true;

    [XmlElement("ProtocolType")]
    public ProtocolType ProtocolType { get; set; }

    [XmlElement("ConnectionTimeout")]
    public int ConnectionTimeout { get; set; } = 5000;

    [XmlElement("UpdateRate")]
    public int UpdateRate { get; set; } = 1000;

    [XmlElement("Description")]
    public string? Description { get; set; }

    [XmlElement("OpcUaConfig")]
    public OpcUaConfig? OpcUaConfig { get; set; }

    [XmlElement("OpcDaConfig")]
    public OpcDaConfig? OpcDaConfig { get; set; }

    [XmlElement("HsmsConfig")]
    public HsmsConfig? HsmsConfig { get; set; }

    [XmlElement("ModbusConfig")]
    public ModbusConfig? ModbusConfig { get; set; }

    [XmlArray("Tags")]
    [XmlArrayItem("Tag")]
    public List<TagConfig> Tags { get; set; } = [];
}

public class OpcUaConfig
{
    [XmlElement("EndpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    [XmlElement("SessionTimeout")]
    public int SessionTimeout { get; set; } = 60000;

    [XmlElement("EnableAutoReconnect")]
    public bool EnableAutoReconnect { get; set; } = true;

    [XmlElement("ReconnectInterval")]
    public int ReconnectInterval { get; set; } = 5000;

    [XmlElement("SkipCertificateValidation")]
    public bool SkipCertificateValidation { get; set; } = true;

    [XmlElement("UseAnonymousAuth")]
    public bool UseAnonymousAuth { get; set; } = true;

    [XmlElement("Username")]
    public string? Username { get; set; }

    [XmlElement("Password")]
    public string? Password { get; set; }
}

public class OpcDaConfig
{
    [XmlElement("ServerProgId")]
    public string ServerProgId { get; set; } = string.Empty;

    [XmlElement("RemoteHost")]
    public string? RemoteHost { get; set; }

    [XmlElement("DeadBand")]
    public float DeadBand { get; set; } = 0.0f;

    [XmlElement("UseAnonymousAuth")]
    public bool UseAnonymousAuth { get; set; } = true;

    [XmlElement("Username")]
    public string? Username { get; set; }

    [XmlElement("Password")]
    public string? Password { get; set; }
}

public enum HsmsMode
{
    Host,
    Eqp
}

public enum HsmsConnectionMode
{
    Active,
    Passive
}

public class HsmsConfig
{
    [XmlElement("Host")]
    public string Host { get; set; } = string.Empty;

    [XmlElement("Port")]
    public int Port { get; set; } = 5000;

    [XmlElement("Mode")]
    public HsmsMode Mode { get; set; } = HsmsMode.Host;

    [XmlElement("ConnectionMode")]
    public HsmsConnectionMode ConnectionMode { get; set; } = HsmsConnectionMode.Active;

    [XmlElement("T3Timeout")]
    public int T3Timeout { get; set; } = 45000;

    [XmlElement("T4Timeout")]
    public int T4Timeout { get; set; } = 10000;

    [XmlElement("T5Timeout")]
    public int T5Timeout { get; set; } = 10000;

    [XmlElement("T6Timeout")]
    public int T6Timeout { get; set; } = 10000;

    [XmlElement("T7Timeout")]
    public int T7Timeout { get; set; } = 10000;

    [XmlElement("ConnectionTimeout")]
    public int ConnectionTimeout { get; set; } = 5000;

    [XmlElement("DeviceType")]
    public string DeviceType { get; set; } = "Simulator";

    [XmlElement("Enable")]
    public bool Enable { get; set; } = true;

    [XmlElement("LinkTestInterval")]
    public int LinkTestInterval { get; set; } = 30000;

    [XmlElement("DeviceId")]
    public int DeviceId { get; set; } = 0;
}

public enum ModbusMode
{
    Tcp,
    Rtu,
    Ascii
}

public class ModbusConfig
{
    [XmlElement("Host")]
    public string Host { get; set; } = string.Empty;

    [XmlElement("Port")]
    public int Port { get; set; } = 502;

    [XmlElement("Mode")]
    public ModbusMode Mode { get; set; } = ModbusMode.Tcp;

    [XmlElement("SlaveId")]
    public byte SlaveId { get; set; } = 1;

    [XmlElement("ConnectionTimeout")]
    public int ConnectionTimeout { get; set; } = 5000;

    [XmlElement("SerialPort")]
    public string SerialPort { get; set; } = "COM1";

    [XmlElement("BaudRate")]
    public int BaudRate { get; set; } = 9600;

    [XmlElement("DataBits")]
    public int DataBits { get; set; } = 8;

    [XmlElement("Parity")]
    public string Parity { get; set; } = "None";

    [XmlElement("StopBits")]
    public int StopBits { get; set; } = 1;

    [XmlElement("ReadTimeout")]
    public int ReadTimeout { get; set; } = 1000;

    [XmlElement("WriteTimeout")]
    public int WriteTimeout { get; set; } = 1000;
}

public enum TagDataType
{
    String,
    Int32,
    Int64,
    Float,
    Double,
    Boolean,
    DateTime
}

public class TagConfig
{
    [XmlElement("Id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Address")]
    public string Address { get; set; } = string.Empty;

    [XmlElement("NodeId")]
    public string NodeId { get; set; } = string.Empty;

    [XmlElement("DataType")]
    public TagDataType DataType { get; set; } = TagDataType.String;

    [XmlElement("ReadOnly")]
    public bool ReadOnly { get; set; } = false;

    [XmlElement("Description")]
    public string? Description { get; set; }

    [XmlElement("MinValue")]
    public string? MinValue { get; set; }

    [XmlElement("MaxValue")]
    public string? MaxValue { get; set; }

    [XmlElement("ScanRate")]
    public int ScanRate { get; set; } = 1000;
}

public class EAPConfiguration
{
    [XmlArray("Devices")]
    [XmlArrayItem("Device")]
    public List<DeviceConfig> Devices { get; set; } = [];

    [XmlElement("GlobalUpdateRate")]
    public int GlobalUpdateRate { get; set; } = 1000;
}

public static class ConfigurationLoader
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ConfigurationLoader));

    public static EAPConfiguration LoadConfiguration(string configDirectory)
    {
        var configuration = new EAPConfiguration();

        try
        {
            if (!Directory.Exists(configDirectory))
            {
                Logger.Warn($"Config directory not found: {configDirectory}");
                return configuration;
            }

            var deviceFolders = Directory.GetDirectories(configDirectory);

            foreach (var folder in deviceFolders)
            {
                // 查找所有 *.config 文件
                var configFiles = Directory.GetFiles(folder, "*.config");
                
                if (configFiles.Length == 0)
                {
                    Logger.Debug($"No config file found in folder: {folder}");
                    continue;
                }
                
                if (configFiles.Length > 1)
                {
                    Logger.Error($"Error: Multiple config files found in folder {folder}: {configFiles.Length} files. Only one config file is allowed.");
                    continue;
                }

                var configFile = configFiles[0];

                if (File.Exists(configFile))
                {
                    try
                    {
                        var deviceConfig = LoadDeviceConfig(configFile);
                        if (deviceConfig != null)
                        {
                            deviceConfig.FolderName = Path.GetFileName(folder);
                            deviceConfig.FilePath = configFile;
                            configuration.Devices.Add(deviceConfig);
                            Logger.Info($"Loaded device config: {deviceConfig.Id} ({deviceConfig.Name}) from {configFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading config file {configFile}: {ex.Message}", ex);
                    }
                }
            }

            Logger.Info($"Loaded {configuration.Devices.Count} device configurations");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading configurations: {ex.Message}", ex);
        }

        return configuration;
    }

    private static DeviceConfig? LoadDeviceConfig(string configFile)
    {
        using var reader = new StreamReader(configFile);
        var serializer = new XmlSerializer(typeof(DeviceConfig));
        return serializer.Deserialize(reader) as DeviceConfig;
    }

    public static void SaveDeviceConfig(DeviceConfig config, string configDirectory)
    {
        try
        {
            var deviceFolder = Path.Combine(configDirectory, config.Id);
            Directory.CreateDirectory(deviceFolder);

            var configFile = Path.Combine(deviceFolder, "device.config");
            using var writer = new StreamWriter(configFile);
            var serializer = new XmlSerializer(typeof(DeviceConfig));
            serializer.Serialize(writer, config);

            Logger.Info($"Saved device config: {configFile}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error saving device config: {ex.Message}", ex);
            throw;
        }
    }

    public static IEnumerable<DeviceConfig> GetDevicesByProtocol(EAPConfiguration config, ProtocolType protocolType)
    {
        return config.Devices.Where(d => d.ProtocolType == protocolType && d.Enabled);
    }

    public static List<TagConfig> LoadTagsByAddress(string configDirectory, string address)
    {
        var tags = new List<TagConfig>();
        
        try
        {
            var configuration = LoadConfiguration(configDirectory);
            foreach (var device in configuration.Devices)
            {
                var matchedTags = device.Tags.Where(t => 
                    !string.IsNullOrEmpty(t.Address) && t.Address.Equals(address, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                tags.AddRange(matchedTags);
            }
            Logger.Info($"Loaded {tags.Count} tags by address: {address}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading tags by address: {ex.Message}", ex);
        }
        
        return tags;
    }

    public static List<TagConfig> LoadTagsByDataType(string configDirectory, TagDataType dataType)
    {
        var tags = new List<TagConfig>();
        
        try
        {
            var configuration = LoadConfiguration(configDirectory);
            foreach (var device in configuration.Devices)
            {
                var matchedTags = device.Tags.Where(t => t.DataType == dataType).ToList();
                tags.AddRange(matchedTags);
            }
            Logger.Info($"Loaded {tags.Count} tags by data type: {dataType}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading tags by data type: {ex.Message}", ex);
        }
        
        return tags;
    }

    public static List<TagConfig> LoadTagsByDeviceId(string configDirectory, string deviceId)
    {
        var tags = new List<TagConfig>();
        
        try
        {
            var configuration = LoadConfiguration(configDirectory);
            var device = configuration.Devices.FirstOrDefault(d => d.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
            if (device != null)
            {
                tags.AddRange(device.Tags);
                Logger.Info($"Loaded {tags.Count} tags for device: {deviceId}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading tags by device id: {ex.Message}", ex);
        }
        
        return tags;
    }
}