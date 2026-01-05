using Confluent.Kafka;
using Newtonsoft.Json;
using SearchService.Models;
using SearchService.Models.Kafka;
using SearchService.Services;

namespace SearchService.BackgroundServices;

public class PlayerConsumerService : BackgroundService
{
    private readonly ILogger<PlayerConsumerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public PlayerConsumerService(
        ILogger<PlayerConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlayerConsumerService is starting...");

        // Wait for Elasticsearch to be ready
        await Task.Delay(5000, stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId = _configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = true
        };

        var topic = _configuration["Kafka:Topics:Players"] ?? "cdc.sport.dbo.players";

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Error}", e.Reason))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("Assigned partitions: [{Partitions}]", string.Join(", ", partitions));
            })
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Subscribed to topic: {Topic}", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                    
                    if (consumeResult == null)
                        continue;

                    _logger.LogDebug("Received message from {Topic}: Key={Key}", 
                        consumeResult.Topic, consumeResult.Message.Key);

                    await ProcessPlayerMessageAsync(consumeResult.Message.Value, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PlayerConsumerService is stopping...");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessPlayerMessageAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(messageValue))
            {
                _logger.LogWarning("Received empty message");
                return;
            }

            var message = JsonConvert.DeserializeObject<DebeziumMessage<PlayerData>>(messageValue);
            
            if (message?.Payload == null)
            {
                _logger.LogWarning("Failed to deserialize message or payload is null");
                return;
            }

            var payload = message.Payload;

            using var scope = _scopeFactory.CreateScope();
            var elasticsearchService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();

            if (payload.IsDelete)
            {
                if (payload.Before != null)
                {
                    _logger.LogInformation("Processing DELETE for player {PlayerId}", payload.Before.PlayerId);
                    await elasticsearchService.DeletePlayerAsync(payload.Before.PlayerId, cancellationToken);
                }
            }
            else if (payload.IsCreate || payload.IsUpdate)
            {
                if (payload.After != null)
                {
                    var operation = payload.IsCreate ? "CREATE" : "UPDATE";
                    _logger.LogInformation("Processing {Operation} for player {PlayerId}: {FirstName} {LastName}", 
                        operation, payload.After.PlayerId, payload.After.FirstName, payload.After.LastName);

                    var playerDocument = MapToPlayerDocument(payload.After);
                    
                    // Try to get existing player to preserve squad info
                    var existingPlayer = await elasticsearchService.GetPlayerAsync(payload.After.PlayerId, cancellationToken);
                    if (existingPlayer != null)
                    {
                        // Preserve existing squad assignments
                        playerDocument.Squads = existingPlayer.Squads;
                    }

                    await elasticsearchService.IndexPlayerAsync(playerDocument, cancellationToken);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing player message: {Message}", messageValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing player message");
        }
    }

    private static PlayerDocument MapToPlayerDocument(PlayerData data)
    {
        return new PlayerDocument
        {
            PlayerId = data.PlayerId,
            FirstName = data.FirstName,
            LastName = data.LastName,
            // io.debezium.time.Date = days since Unix epoch
            DateOfBirth = data.DateOfBirth.HasValue 
                ? DateTime.UnixEpoch.AddDays(data.DateOfBirth.Value) 
                : null,
            Nationality = data.Nationality,
            Position = data.Position,
            JerseyNumber = data.JerseyNumber,
            HeightCm = data.HeightCm,
            WeightKg = data.WeightKg,
            // io.debezium.time.NanoTimestamp = nanoseconds since Unix epoch
            CreatedAt = data.CreatedAt.HasValue 
                ? DateTime.UnixEpoch.AddTicks(data.CreatedAt.Value / 100) 
                : null,
            UpdatedAt = data.UpdatedAt.HasValue 
                ? DateTime.UnixEpoch.AddTicks(data.UpdatedAt.Value / 100) 
                : null,
            Squads = [] // Will be populated from squad events
        };
    }
}
