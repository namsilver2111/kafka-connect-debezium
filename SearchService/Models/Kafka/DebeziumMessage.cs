using Newtonsoft.Json;

namespace SearchService.Models.Kafka;

/// <summary>
/// Full Debezium CDC message with schema and payload
/// </summary>
public class DebeziumMessage<T> where T : class
{
    [JsonProperty("schema")]
    public DebeziumSchema? Schema { get; set; }

    [JsonProperty("payload")]
    public DebeziumPayload<T>? Payload { get; set; }
}

/// <summary>
/// Debezium message payload containing the actual CDC data
/// </summary>
public class DebeziumPayload<T> where T : class
{
    [JsonProperty("before")]
    public T? Before { get; set; }

    [JsonProperty("after")]
    public T? After { get; set; }

    [JsonProperty("source")]
    public DebeziumSource? Source { get; set; }

    [JsonProperty("op")]
    public string? Operation { get; set; }

    [JsonProperty("ts_ms")]
    public long? TimestampMs { get; set; }

    [JsonProperty("transaction")]
    public DebeziumTransaction? Transaction { get; set; }

    /// <summary>
    /// Operation types: c = create, u = update, d = delete, r = read (snapshot)
    /// </summary>
    public bool IsCreate => Operation == "c" || Operation == "r";
    public bool IsUpdate => Operation == "u";
    public bool IsDelete => Operation == "d";
}

/// <summary>
/// Debezium schema metadata (usually not needed for processing)
/// </summary>
public class DebeziumSchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("fields")]
    public List<DebeziumField>? Fields { get; set; }

    [JsonProperty("optional")]
    public bool Optional { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("version")]
    public int? Version { get; set; }
}

/// <summary>
/// Debezium schema field definition
/// </summary>
public class DebeziumField
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("fields")]
    public List<DebeziumField>? Fields { get; set; }

    [JsonProperty("optional")]
    public bool Optional { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("version")]
    public int? Version { get; set; }

    [JsonProperty("field")]
    public string? Field { get; set; }

    [JsonProperty("default")]
    public object? Default { get; set; }

    [JsonProperty("parameters")]
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Debezium source metadata (SQL Server connector)
/// </summary>
public class DebeziumSource
{
    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("connector")]
    public string Connector { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("ts_ms")]
    public long TimestampMs { get; set; }

    [JsonProperty("snapshot")]
    public string? Snapshot { get; set; }

    [JsonProperty("db")]
    public string Database { get; set; } = string.Empty;

    [JsonProperty("sequence")]
    public string? Sequence { get; set; }

    [JsonProperty("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonProperty("table")]
    public string Table { get; set; } = string.Empty;

    [JsonProperty("change_lsn")]
    public string? ChangeLsn { get; set; }

    [JsonProperty("commit_lsn")]
    public string? CommitLsn { get; set; }

    [JsonProperty("event_serial_no")]
    public long? EventSerialNo { get; set; }
}

/// <summary>
/// Debezium transaction metadata
/// </summary>
public class DebeziumTransaction
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("total_order")]
    public long TotalOrder { get; set; }

    [JsonProperty("data_collection_order")]
    public long DataCollectionOrder { get; set; }
}

/// <summary>
/// Player data from CDC
/// </summary>
public class PlayerData
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Days since Unix epoch (io.debezium.time.Date)
    /// </summary>
    [JsonProperty("date_of_birth")]
    public int? DateOfBirth { get; set; }

    [JsonProperty("nationality")]
    public string? Nationality { get; set; }

    [JsonProperty("position")]
    public string? Position { get; set; }

    [JsonProperty("jersey_number")]
    public int? JerseyNumber { get; set; }

    [JsonProperty("height_cm")]
    public int? HeightCm { get; set; }

    [JsonProperty("weight_kg")]
    public int? WeightKg { get; set; }

    [JsonProperty("created_at")]
    public long? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public long? UpdatedAt { get; set; }
}

/// <summary>
/// Squad data from CDC
/// </summary>
public class SquadData
{
    [JsonProperty("squad_id")]
    public int SquadId { get; set; }

    [JsonProperty("team_id")]
    public int TeamId { get; set; }

    [JsonProperty("player_id")]
    public int PlayerId { get; set; }

    /// <summary>
    /// Days since Unix epoch (io.debezium.time.Date)
    /// </summary>
    [JsonProperty("join_date")]
    public int? JoinDate { get; set; }

    /// <summary>
    /// Days since Unix epoch (io.debezium.time.Date)
    /// </summary>
    [JsonProperty("leave_date")]
    public int? LeaveDate { get; set; }

    /// <summary>
    /// Base64-encoded Kafka Connect Decimal (org.apache.kafka.connect.data.Decimal with scale=2)
    /// </summary>
    [JsonProperty("contract_value")]
    public string? ContractValue { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }

    [JsonProperty("created_at")]
    public long? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public long? UpdatedAt { get; set; }

    /// <summary>
    /// Converts the Base64-encoded Kafka Connect Decimal to a decimal value.
    /// Kafka Connect Decimal is a big-endian signed integer divided by 10^scale.
    /// </summary>
    public decimal? GetContractValueAsDecimal(int scale = 2)
    {
        if (string.IsNullOrEmpty(ContractValue))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(ContractValue);
            
            // Kafka Connect Decimal is a signed big-endian integer
            // We need to convert it to a decimal by dividing by 10^scale
            var isNegative = (bytes[0] & 0x80) != 0;
            
            // Convert big-endian bytes to BigInteger
            // .NET BigInteger expects little-endian, so reverse the array
            var reversedBytes = new byte[bytes.Length + 1];
            for (int i = 0; i < bytes.Length; i++)
            {
                reversedBytes[i] = bytes[bytes.Length - 1 - i];
            }
            // Add sign byte for positive numbers to prevent interpretation as negative
            reversedBytes[bytes.Length] = 0;
            
            var bigInt = new System.Numerics.BigInteger(reversedBytes);
            
            // If original was negative (two's complement), adjust
            if (isNegative)
            {
                var maxValue = System.Numerics.BigInteger.Pow(2, bytes.Length * 8);
                bigInt = bigInt - maxValue;
            }
            
            // Divide by 10^scale to get the decimal value
            var divisor = (decimal)Math.Pow(10, scale);
            return (decimal)bigInt / divisor;
        }
        catch
        {
            return null;
        }
    }
}
