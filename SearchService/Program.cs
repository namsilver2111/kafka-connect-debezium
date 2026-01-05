using SearchService.BackgroundServices;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Player Search Service", 
        Version = "v1",
        Description = "Search service for players using Elasticsearch, powered by CDC events from Kafka"
    });
});

// Register Elasticsearch service
builder.Services.AddSingleton<IElasticsearchService, ElasticsearchService>();

// Register Kafka consumer background services
builder.Services.AddHostedService<PlayerConsumerService>();
builder.Services.AddHostedService<SquadConsumerService>();

var app = builder.Build();

// Create Elasticsearch index on startup
using (var scope = app.Services.CreateScope())
{
    var elasticsearchService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Creating Elasticsearch index...");
    
    // Retry logic for Elasticsearch connection
    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(5);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var success = await elasticsearchService.CreateIndexAsync();
            if (success)
            {
                logger.LogInformation("Elasticsearch index created successfully");
                break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create Elasticsearch index (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
        }
        
        if (i < maxRetries - 1)
        {
            logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Player Search Service v1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthorization();
app.MapControllers();

app.Run();

