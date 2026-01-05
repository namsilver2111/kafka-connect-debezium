using Confluent.Kafka;
using Newtonsoft.Json;
using SearchService.Models;
using SearchService.Models.Kafka;
using SearchService.Services;

namespace SearchService.BackgroundServices;

public class SquadConsumerService : BackgroundService
{
    private readonly ILogger<SquadConsumerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public SquadConsumerService(
        ILogger<SquadConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SquadConsumerService is starting...");

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

        var topic = _configuration["Kafka:Topics:Squad"] ?? "cdc.sport.dbo.squad";

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

                    await ProcessSquadMessageAsync(consumeResult.Message.Value, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SquadConsumerService is stopping...");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessSquadMessageAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(messageValue))
            {
                _logger.LogWarning("Received empty message");
                return;
            }

            var message = JsonConvert.DeserializeObject<DebeziumMessage<SquadData>>(messageValue);
            
            if (message?.Payload == null)
            {
                _logger.LogWarning("Failed to deserialize squad message or payload is null");
                return;
            }

            var payload = message.Payload;

            using var scope = _scopeFactory.CreateScope();
            var elasticsearchService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();

            if (payload.IsDelete)
            {
                if (payload.Before != null)
                {
                    _logger.LogInformation("Processing DELETE for squad {SquadId}, player {PlayerId}", 
                        payload.Before.SquadId, payload.Before.PlayerId);
                    
                    // Remove squad from player
                    await elasticsearchService.RemovePlayerSquadAsync(
                        payload.Before.PlayerId,
                        payload.Before.SquadId,
                        cancellationToken);
                }
            }
            else if (payload.IsCreate || payload.IsUpdate)
            {
                if (payload.After != null)
                {
                    var operation = payload.IsCreate ? "CREATE" : "UPDATE";
                    _logger.LogInformation("Processing {Operation} for squad {SquadId}, player {PlayerId}, team {TeamId}", 
                        operation, payload.After.SquadId, payload.After.PlayerId, payload.After.TeamId);

                    var squadInfo = new SquadInfo
                    {
                        SquadId = payload.After.SquadId,
                        TeamId = payload.After.TeamId,
                        // io.debezium.time.Date = days since Unix epoch
                        JoinDate = payload.After.JoinDate.HasValue 
                            ? DateTime.UnixEpoch.AddDays(payload.After.JoinDate.Value) 
                            : null,
                        LeaveDate = payload.After.LeaveDate.HasValue 
                            ? DateTime.UnixEpoch.AddDays(payload.After.LeaveDate.Value) 
                            : null,
                        ContractValue = payload.After.GetContractValueAsDecimal(),
                        IsActive = payload.After.IsActive
                    };

                    await elasticsearchService.UpsertPlayerSquadAsync(
                        payload.After.PlayerId,
                        squadInfo,
                        cancellationToken);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing squad message: {Message}", messageValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing squad message");
        }
    }
}
