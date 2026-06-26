using System;
using EAP.Adapters.Hsms;
using EAP.Adapters.Modbus;
using EAP.Adapters.OpcDa;
using EAP.Adapters.OpcUa;
using EAP.Core;

namespace EAP.Adapters.Factory;

public static class ProtocolClientFactory
{
    public static IProtocolClient CreateClient(DeviceConfig config)
    {
        return config.ProtocolType switch
        {
            ProtocolType.OpcUa => new OpcUaClient(config),
            ProtocolType.OpcDa => new OpcDaClient(config),
            ProtocolType.Hsms => new HsmsClient(config),
            ProtocolType.Modbus => new ModbusClient(config),
            _ => throw new NotSupportedException($"Unsupported protocol type: {config.ProtocolType}")
        };
    }
}