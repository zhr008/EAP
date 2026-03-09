using Cim.Core.Interfaces;
using Cim.Core.Models;
using Cim.MasterService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// gRPC Services
builder.Services.AddGrpc();

// Core Services
builder.Services.AddSingleton<IEquipmentStateService, EquipmentStateService>();
builder.Services.AddSingleton<IMessagePublisher, KafkaMessagePublisher>();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// gRPC endpoints
app.MapGrpcService<EquipmentGrpcService>();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Register sample equipment
var stateService = app.Services.GetRequiredService<IEquipmentStateService>();
stateService.RegisterEquipment(new EquipmentInfo
{
    EquipmentId = "EQP001",
    EquipmentType = "ETCHER",
    HostAddress = "192.168.1.100",
    Port = 5000,
    DeviceId = 1,
    State = EquipmentState.Offline
});

stateService.RegisterEquipment(new EquipmentInfo
{
    EquipmentId = "EQP002",
    EquipmentType = "DEPOSITOR",
    HostAddress = "192.168.1.101",
    Port = 5000,
    DeviceId = 2,
    State = EquipmentState.Offline
});

Console.WriteLine("===========================================");
Console.WriteLine("CIM Master Service Started");
Console.WriteLine($"gRPC Endpoint: https://localhost:7001");
Console.WriteLine($"REST API: https://localhost:7000/api");
Console.WriteLine($"Swagger UI: https://localhost:7000/swagger");
Console.WriteLine("===========================================");

app.Run();
