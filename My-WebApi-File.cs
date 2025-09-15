
//My Web-Api Code 

using Azure.Messaging.ServiceBus;
using FleetGPSAPI.Data;
using FleetGPSAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FleetGPSAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GPSController : ControllerBase
    {
        private readonly FleetDbContext _context;
        private readonly IConfiguration _config;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly string _queueName;

        public GPSController(FleetDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            _serviceBusClient = new ServiceBusClient(_config["ServiceBus:ConnectionString"]);
            _queueName = _config["ServiceBus:QueueName"];
        }

        [HttpPost("send")]
        public async Task<IActionResult> ReceiveGPSData([FromBody] GPSDataRequest request)
        {
            var data = new GPSData
            {
                VehicleId = request.VehicleId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Timestamp = DateTime.UtcNow
            };

            // Optional: Save to DB immediately (if needed for fast availability)
            // _context.GPSData.Add(data);
            // await _context.SaveChangesAsync();

            // Send to Azure Service Bus Queue
            try
            {
                await SendToQueue(data);
                return Ok(new { message = "Data sent to queue", data });
            }
            catch (Exception ex)
            {
                // log error, etc.
                return StatusCode(500, new { error = "Failed to send message to Service Bus", details = ex.Message });
            }

        }

        private async Task SendToQueue(GPSData data)
        {
            var sender = _serviceBusClient.CreateSender(_queueName);
            var json = JsonSerializer.Serialize(data);
            var message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
    }
}

//appsettings.cs

{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:fleetdb123.database.windows.net,1433;Initial Catalog=FleetDb;Persist Security Info=False;User ID=fleetadmin;Password=pass;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=fleetstorage123;AccountKey=a1VHH3SDk/Q2eE5DHZP/cB6al3c3VswnrXnpfZYihrnFMlr2wUuPmQo6vgBz8QIL/gFA==;EndpointSuffix=core.windows.net"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "alfrednrmlgmail.onmicrosoft.com",
    "TenantId": "8c7f78f3-0520-44bc-8720-d556c35bb4c1",
    "ClientId": "b680573d-4a86-4905-99f4-d1bc5d66cf2e",
    "CallbackPath": "/swagger/oauth2-redirect.html" // optional, but good to include
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://fleetbus123.servicebus.windows.net/;SharedAccessKeyName=GPSQueueAccess;SharedAccessKey=y9thwgusyXWCvbC/v4x5f4TeKPK+ASbBg8agg=;EntityPath=gpsqueue",
    "QueueName": "gpsqueue"
  }


}
//Program.cs

using FleetGPSAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add authentication with Microsoft Identity Platform
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// ✅ Add authorization
builder.Services.AddAuthorization();

// ✅ Add EF Core
builder.Services.AddDbContext<FleetDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Add controllers
builder.Services.AddControllers();

// ✅ Swagger + OAuth2
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FleetGPSAPI", Version = "v1" });

    // OAuth2 security definition
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("https://login.microsoftonline.com/8c7f78f3-0520-44bc-8720-d556c35bb4c1/oauth2/v2.0/authorize"),
                TokenUrl = new Uri("https://login.microsoftonline.com/8c7f78f3-0520-44bc-8720-d556c35bb4c1/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "api://b680573d-4a86-4905-99f4-d1bc5d66cf2e/access_as_user", "Access Fleet GPS API" }
                }
            }
        }
    });

    // OAuth2 security requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "api://b680573d-4a86-4905-99f4-d1bc5d66cf2e/access_as_user" }
        }
    });
});

var app = builder.Build();

// ✅ Swagger UI setup
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FleetGPSAPI v1");

    // 🔑 OAuth2 Client Info
    c.OAuthClientId("b680573d-4a86-4905-99f4-d1bc5d66cf2e");
    c.OAuthUsePkce(); // Use PKCE for Authorization Code flow
    c.OAuthScopeSeparator(" ");
    // ❌ NO `resource` param, as this breaks v2.0 flow
    // ❌ DO NOT add c.OAuthAdditionalQueryStringParams(...)
});

app.MapGet("/", () => Results.Redirect("/swagger"));

// ✅ Auth middleware
app.UseHttpsRedirection();
app.UseAuthentication(); // <- Required
app.UseAuthorization();
app.MapControllers();

app.Run();


// Data Models

using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetGPSAPI.Models
{
    public class GPSData
    {
        public Guid Id { get; set; } = Guid.NewGuid();// EF Core primary key
        public string VehicleId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

