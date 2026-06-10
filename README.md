# Debezium CDC Setup - How to Run

This guide explains how to set up and run Debezium Change Data Capture (CDC) with SQL Server, Kafka, PostgreSQL, and Elasticsearch.

---

## Architecture Overview

```
┌──────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌────────────────┐
│  SQL Server  │───▶│   Debezium   │───▶│      Kafka      │───▶│   PostgreSQL   │
│   (Source)   │    │   Connector  │    │                 │    │   (via Sink)   │
└──────────────┘    └──────────────┘    └─────────────────┘    └────────────────┘
                                               │
                                               ▼
                                        ┌─────────────────┐    ┌────────────────┐
                                        │  SearchService  │───▶│ Elasticsearch  │
                                        │   (.NET 8.0)    │    │                │
                                        └─────────────────┘    └────────────────┘
```

**Data Flow:**
1. CDC events captured from SQL Server by Debezium source connectors
2. Events published to Kafka topics
3. Sink connectors write data to PostgreSQL
4. SearchService consumes Kafka events and indexes to Elasticsearch

---

## Prerequisites

- Docker & Docker Compose installed
- SQL Server instance (or use Docker)
- Minimum 8GB RAM available for Docker

---

## Project Structure

```
Debezium/
├── docker-compose.yml              # All infrastructure services
├── kafka-connect.http              # HTTP requests for connector management
├── script/
│   ├── init_sqlserver.sql          # SQL Server database setup with CDC
│   └── init_postgres.sql           # PostgreSQL target database setup
├── connectors/
│   ├── source/                     # Source connector configs (SQL Server → Kafka)
│   │   ├── teams-source-connector.json
│   │   ├── players-source-connector.json
│   │   └── squad-source-connector.json
│   └── sink/                       # Sink connector configs (Kafka → PostgreSQL)
│       ├── teams-sink-connector.json
│       ├── players-sink-connector.json
│       └── squad-sink-connector.json
├── SearchService/                  # .NET 8.0 Search API
│   ├── Program.cs
│   ├── Dockerfile
│   ├── Controllers/
│   │   ├── HealthController.cs
│   │   └── PlayersController.cs
│   ├── BackgroundServices/
│   │   ├── PlayerConsumerService.cs
│   │   └── SquadConsumerService.cs
│   ├── Services/
│   │   ├── IElasticsearchService.cs
│   │   └── ElasticsearchService.cs
│   └── Models/
│       ├── PlayerDocument.cs
│       └── Kafka/
│           └── DebeziumMessage.cs
└── README.md
```

---

## Step 1: Start Kafka Infrastructure

```bash
# Navigate to project directory
cd Debezium

# Start all services
docker-compose up -d

# Verify services are running
docker-compose ps
```

### Services Started:

| Service | Port | URL | Description |
|---------|------|-----|-------------|
| Zookeeper | 2181 | - | Kafka coordination |
| Kafka | 9092, 29092 | - | Message broker |
| Kafka Connect | 8083 | http://localhost:8083 | Debezium connectors |
| Kafka UI | 8080 | http://localhost:8080 | Kafka monitoring UI |
| PostgreSQL | 5432 | - | Target database (sink) |
| pgAdmin | 5050 | http://localhost:5050 | PostgreSQL management UI |
| Elasticsearch | 9200, 9300 | http://localhost:9200 | Search engine |
| Kibana | 5601 | http://localhost:5601 | Elasticsearch UI |

### Wait for Services to be Ready

```bash
# Check Kafka Connect is ready (wait ~30-60 seconds)
curl http://localhost:8083/connectors

# Check Elasticsearch is ready
curl http://localhost:9200/_cluster/health
```

Expected response for connectors: `[]` (empty array)

---

## Step 2: Setup SQL Server Database

### Option A: Using Existing SQL Server

1. Connect to your SQL Server using SSMS or Azure Data Studio
2. Run the script: `script/init_sqlserver.sql`

### Option B: Add SQL Server to Docker Compose

Add this service to `docker-compose.yml`:

```yaml
sqlserver:
  image: mcr.microsoft.com/mssql/server:2022-latest
  container_name: sqlserver
  hostname: sqlserver
  ports:
    - "1433:1433"
  environment:
    ACCEPT_EULA: "Y"
    MSSQL_SA_PASSWORD: "YourStrong@Passw0rd"
    MSSQL_AGENT_ENABLED: "true"
  networks:
    - debezium-network
```

Then run:
```bash
docker-compose up -d sqlserver

# Wait for SQL Server to start, then run init script
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C -i /path/to/init_sqlserver.sql
```

### What the Script Creates:

- **Database**: `sport`
- **Tables**: `teams`, `players`, `squad`
- **User**: `debezium_user` (Password: `Debezium@2025!`)
- **CDC**: Enabled on all 3 tables

---

## Step 3: Register Source Connectors

Source connectors capture changes from SQL Server and publish to Kafka.

### Register All Source Connectors

```bash
# Register teams source connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/source/teams-source-connector.json

# Register players source connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/source/players-source-connector.json

# Register squad source connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/source/squad-source-connector.json
```

### Verify Source Connectors

```bash
# List all connectors
curl http://localhost:8083/connectors

# Check specific connector status
curl http://localhost:8083/connectors/sqlserver-teams-source-connector/status
curl http://localhost:8083/connectors/sqlserver-players-source-connector/status
curl http://localhost:8083/connectors/sqlserver-squad-source-connector/status
```

---

## Step 4: Register Sink Connectors

Sink connectors consume from Kafka and write to PostgreSQL.

### Register All Sink Connectors

```bash
# Register teams sink connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/sink/teams-sink-connector.json

# Register players sink connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/sink/players-sink-connector.json

# Register squad sink connector
curl -X POST http://localhost:8083/connectors \
  -H "Content-Type: application/json" \
  -d @connectors/sink/squad-sink-connector.json
```

### Verify Sink Connectors

```bash
curl http://localhost:8083/connectors/postgres-teams-sink-connector/status
curl http://localhost:8083/connectors/postgres-players-sink-connector/status
curl http://localhost:8083/connectors/postgres-squad-sink-connector/status
```

---

## Step 5: Run SearchService (Optional)

The SearchService is a .NET 8.0 API that consumes Kafka events and indexes player data to Elasticsearch.

### Run Locally

```bash
cd SearchService
dotnet run
```

### Run with Docker

```bash
cd SearchService
docker build -t search-service .
docker run -d \
  --name search-service \
  --network debezium-network \
  -p 8081:8080 \
  -e Kafka__BootstrapServers=kafka:29092 \
  -e Elasticsearch__Url=http://elasticsearch:9200 \
  search-service
```

### SearchService Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Swagger UI |
| `/api/players/search` | GET | Search players |
| `/api/health` | GET | Health check |

---

## Step 6: Verify CDC is Working

### Check Kafka Topics

```bash
# List all topics
docker exec kafka kafka-topics --bootstrap-server localhost:9092 --list
```

Expected topics:
- `cdc.sport.dbo.teams`
- `cdc.sport.dbo.players`
- `cdc.sport.dbo.squad`

### Consume Messages

```bash
# Watch teams changes
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic cdc.sport.dbo.teams \
  --from-beginning

# Watch players changes
docker exec kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic cdc.sport.dbo.players \
  --from-beginning
```

### Verify Data in PostgreSQL

```bash
# Connect to PostgreSQL
docker exec -it postgres psql -U postgres -d sport

# Check tables
SELECT * FROM teams;
SELECT * FROM players;
SELECT * FROM squad;
```

### Verify Data in Elasticsearch

```bash
# Check players index
curl http://localhost:9200/players/_search?pretty
```

### Using UI Tools

- **Kafka UI**: http://localhost:8080 - View topics, messages, and connectors
- **pgAdmin**: http://localhost:5050 - Manage PostgreSQL
- **Kibana**: http://localhost:5601 - Explore Elasticsearch data

---

## Step 7: Test CDC with Data Changes

Run these SQL commands in SQL Server to test CDC:

```sql
USE sport;

-- Insert new team
INSERT INTO teams (team_name, city, country, founded_year, stadium)
VALUES ('Inter Milan', 'Milan', 'Italy', 1908, 'San Siro');

-- Update existing team
UPDATE teams SET stadium = 'New Old Trafford' WHERE team_id = 1;

-- Insert new player
INSERT INTO players (first_name, last_name, nationality, position, jersey_number)
VALUES ('Erling', 'Haaland', 'Norway', 'Forward', 9);

-- Delete a record
DELETE FROM squad WHERE squad_id = 1;
```

Watch the changes propagate:
1. Kafka topics (via Kafka UI or console consumer)
2. PostgreSQL tables (via pgAdmin or psql)
3. Elasticsearch index (via Kibana or curl)

---

## Useful Commands

### Docker Commands

```bash
# View logs
docker-compose logs -f kafka-connect

# Restart a service
docker-compose restart kafka-connect

# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

### Connector Management

```bash
# Pause connector
curl -X PUT http://localhost:8083/connectors/sqlserver-teams-source-connector/pause

# Resume connector
curl -X PUT http://localhost:8083/connectors/sqlserver-teams-source-connector/resume

# Delete connector
curl -X DELETE http://localhost:8083/connectors/sqlserver-teams-source-connector

# Restart connector
curl -X POST http://localhost:8083/connectors/sqlserver-teams-source-connector/restart
```

---

## Troubleshooting

### Connector Failed to Start

1. Check connector status:
   ```bash
   curl http://localhost:8083/connectors/sqlserver-teams-source-connector/status | jq
   ```

2. Check Kafka Connect logs:
   ```bash
   docker-compose logs kafka-connect | tail -100
   ```

### Common Issues

| Issue | Solution |
|-------|----------|
| Connection refused to SQL Server | Verify SQL Server hostname/port in connector config |
| CDC not enabled error | Run `init_sqlserver.sql` script first |
| Permission denied | Check `debezium_user` has proper CDC permissions |
| Kafka Connect not ready | Wait 30-60 seconds after starting containers |
| Elasticsearch connection failed | Wait for Elasticsearch to be healthy |
| Sink connector not writing | Check PostgreSQL connection and table schema |

### Verify SQL Server CDC Status

```sql
-- Check if CDC is enabled on database
SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'sport';

-- Check which tables have CDC enabled
SELECT name FROM sys.tables WHERE is_tracked_by_cdc = 1;
```

---

## Configuration Reference

### Debezium User Credentials (SQL Server)

| Setting | Value |
|---------|-------|
| Username | `debezium_user` |
| Password | `Debezium@2025!` |

### PostgreSQL Credentials

| Setting | Value |
|---------|-------|
| Host | `localhost` (or `postgres` in Docker network) |
| Port | `5432` |
| Database | `sport` |
| Username | `postgres` |
| Password | `postgres` |

### pgAdmin Credentials

| Setting | Value |
|---------|-------|
| URL | http://localhost:5050 |
| Email | `admin@admin.com` |
| Password | `admin` |

### Kafka Topics

| Topic | Source Table | Description |
|-------|--------------|-------------|
| `cdc.sport.dbo.teams` | `dbo.teams` | Team changes |
| `cdc.sport.dbo.players` | `dbo.players` | Player changes |
| `cdc.sport.dbo.squad` | `dbo.squad` | Squad changes |

---

## Next Steps

1. ✅ ~~Add sink connectors to write data to target systems~~ (Implemented)
2. ✅ ~~Add Elasticsearch for search functionality~~ (Implemented)
3. Configure Schema Registry for Avro serialization
4. ✅ ~~Set up monitoring with Prometheus/Grafana~~ (Implemented)
5. Configure production-ready settings (replication, security, SSL/TLS)