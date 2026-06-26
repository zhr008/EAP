using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EAP.Core;

public enum ProtocolType
{
    OpcUa,
    OpcDa,
    Hsms,
    Modbus
}

public class DeviceConfig
{
    [XmlElement("DeviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [XmlElement("DeviceName")]
    public string DeviceName { get; set; } = string.Empty;

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

    [XmlElement("T8Timeout")]
    public int T8Timeout { get; set; } = 100000;

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