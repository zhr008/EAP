using Cim.AgentService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHostedService<DeviceAgentService>();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Agent info endpoint
app.MapGet("/info", (IConfiguration config) => 
    Results.Ok(new { 
        AgentId = config["Agent:Id"],
        MachineName = Environment.MachineName,
        Timestamp = DateTime.UtcNow 
    }));

app.Run();
