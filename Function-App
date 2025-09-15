using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ProcessGPSFunction.Models;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessGPSFunction
{
    public class GPSProcessor
    {
        private readonly ILogger _logger;
        private static readonly HttpClient _httpClient = new HttpClient();
        public GPSProcessor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GPSProcessor>();
        }

        [Function("ProcessGPSFunction")]
        public async Task Run([ServiceBusTrigger("gpsqueue", Connection = "ServiceBusConnection")] string myQueueItem)
        {
            _logger.LogInformation($"📦 Processing GPS data: {myQueueItem}");

            try
            {
                var data = JsonSerializer.Deserialize<GPSData>(myQueueItem);

                if (data != null)
                {
                    // ✅ Non-intrusive: Just logs abnormal data; doesn't block insert
                    if (data.Latitude > 90 || data.Latitude < -90 || data.Longitude > 180 || data.Longitude < -180)
                    {
                        _logger.LogWarning($"🚨 Invalid GPS Detected! VehicleId: {data.VehicleId}, Latitude: {data.Latitude}, Longitude: {data.Longitude}, Timestamp: {data.Timestamp}");
                        await NotifyInvalidGPSAsync(data);
                    }

                    await SaveToDatabase(data);

                    _logger.LogInformation($"✅ GPS data for vehicle {data.VehicleId} stored successfully.");
                }
                else
                {
                    _logger.LogError("❌ Failed to deserialize GPS data.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"💥 Error processing GPS data: {ex.Message}");
            }
        }

        private async Task SaveToDatabase(GPSData data)
        {
            string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var command = new SqlCommand(
                "INSERT INTO GPSData (VehicleId, Latitude, Longitude, Timestamp) VALUES (@vehicleId, @latitude, @longitude, @timestamp)",
                connection);

            command.Parameters.AddWithValue("@vehicleId", data.VehicleId);
            command.Parameters.AddWithValue("@latitude", data.Latitude);
            command.Parameters.AddWithValue("@longitude", data.Longitude);
            command.Parameters.AddWithValue("@timestamp", data.Timestamp);

            await command.ExecuteNonQueryAsync();
        }
        private async Task NotifyInvalidGPSAsync(GPSData data)
        {
            var logicAppUrl = Environment.GetEnvironmentVariable("LogicAppUrl");
            if (string.IsNullOrEmpty(logicAppUrl))
            {
                _logger.LogError("❌ LogicAppUrl not configured in environment variables.");
                return;
            }

            var payload = new
            {
                data.VehicleId,
                data.Latitude,
                data.Longitude,
                data.Timestamp
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(logicAppUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"📧 Notification sent to Logic App for Vehicle {data.VehicleId}.");
            }
            else
            {
                _logger.LogError($"❌ Failed to notify Logic App. Status Code: {response.StatusCode}");
            }
        }
    }
}
