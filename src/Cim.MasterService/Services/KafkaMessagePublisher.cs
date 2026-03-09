using Cim.Core.Models;
using Cim.Core.Interfaces;

namespace Cim.MasterService.Services;

/// <summary>
/// Kafka 消息发布服务实现
/// </summary>
public class KafkaMessagePublisher : IMessagePublisher
{
    private readonly ILogger<KafkaMessagePublisher> _logger;
    private readonly string _bootstrapServers;
    private readonly bool _kafkaEnabled;

    public KafkaMessagePublisher(
        ILogger<KafkaMessagePublisher> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        _kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", false);
        
        if (_kafkaEnabled)
        {
            InitializeKafkaProducer();
        }
    }

    private void InitializeKafkaProducer()
    {
        // TODO: 初始化 Confluent.Kafka 生产者
        // var config = new ProducerConfig { BootstrapServers = _bootstrapServers };
        // _producer = new ProducerBuilder<Null, string>(config).Build();
        _logger.LogInformation("Kafka producer initialized with servers: {Servers}", _bootstrapServers);
    }

    public async Task PublishAsync<T>(string topic, T message)
    {
        if (!_kafkaEnabled)
        {
            _logger.LogDebug("Kafka disabled. Message to {Topic}: {@Message}", topic, message);
            return;
        }

        try
        {
            // TODO: 使用 Confluent.Kafka 发送消息
            // var json = JsonSerializer.Serialize(message);
            // await _producer.ProduceAsync(topic, new Message<Null, string> { Value = json });
            
            _logger.LogDebug("Published to {Topic}: {@Message}", topic, message);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to Kafka topic {Topic}", topic);
        }
    }

    public async Task PublishEquipmentStateChangeAsync(string equipmentId, EquipmentState previousState, EquipmentState newState, string? reason = null)
    {
        var message = new
        {
            EventType = "EquipmentStateChanged",
            EquipmentId = equipmentId,
            PreviousState = previousState.ToString(),
            NewState = newState.ToString(),
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        await PublishAsync("cim.equipment.state", message);
    }

    public async Task PublishAlarmEventAsync(string alarmId, string equipmentId, AlarmInfo alarmInfo, bool isRaised)
    {
        var message = new
        {
            EventType = isRaised ? "AlarmRaised" : "AlarmCleared",
            AlarmId = alarmId,
            EquipmentId = equipmentId,
            AlarmCode = alarmInfo.AlarmCode,
            Description = alarmInfo.Description,
            Severity = alarmInfo.Severity.ToString(),
            Timestamp = DateTime.UtcNow
        };

        await PublishAsync("cim.alarm.events", message);
    }

    public async Task PublishEventReportAsync(string equipmentId, EventReport report)
    {
        var message = new
        {
            EventType = "EventReport",
            EquipmentId = equipmentId,
            EventId_SECS = report.EventId_SECS,
            EventType = report.EventType,
            DataItems = report.DataItems,
            Timestamp = DateTime.UtcNow
        };

        await PublishAsync("cim.event.reports", message);
    }

    public async Task PublishDataCollectionAsync(string equipmentId, IEnumerable<DataCollectionItem> items)
    {
        var message = new
        {
            EventType = "DataCollection",
            EquipmentId = equipmentId,
            Items = items.Select(i => new
            {
                i.VariableId,
                i.VariableName,
                Value = i.Value?.ToString(),
                i.DataType,
                i.Unit,
                i.Timestamp
            }),
            Timestamp = DateTime.UtcNow
        };

        await PublishAsync("cim.data.collection", message);
    }
}
