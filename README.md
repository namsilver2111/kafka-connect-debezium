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

## Project Setup (Quick Start)

Follow these 3 steps in order to set up and run the project:

### Step 1: Run SQL Server Database Setup Script
Initialize the source database schema, populate initial data, enable CDC, and configure Debezium credentials by running the [script/init_sqlserver.sql](file:///c:/Users/NamDang/Documents/kafka-connect-debezium/script/init_sqlserver.sql) script.

#### Using an existing SQL Server instance
Connect to your database engine via SSMS, Azure Data Studio, or any CLI tool and execute the script [script/init_sqlserver.sql].

> [!NOTE]
> The database initialization script configures the following:
> - Creates `sport` database and `teams`, `players`, `squad` tables.
> - Creates login user `debezium_user` (Password: `Debezium@2025!`) and grants required permissions.
> - Enables CDC on the database and all three tables.

---

### Step 2: Start Infrastructure Services
Start the rest of the application ecosystem configured in [docker-compose.yml](file:///c:/Users/NamDang/Documents/kafka-connect-debezium/docker-compose.yml):

```bash
# Start all containers in the background
docker-compose up -d

# Verify that all services are healthy and running
docker-compose ps
```

#### Services Started:
| Service | Port | External URL | Description |
|---|---|---|---|
| **Zookeeper** | `2181` | - | Kafka coordination |
| **Kafka** | `9092` / `29092` | - | Message Broker |
| **Kafka Connect** | `8083` | http://localhost:8083 | Debezium connectors engine |
| **Kafka UI** | `8080` | http://localhost:8080 | Kafka monitoring dashboard |
| **PostgreSQL** | `5432` | - | Target database (sink) |
| **pgAdmin** | `5050` | http://localhost:5050 | PostgreSQL UI editor |
| **Elasticsearch** | `9200` | http://localhost:9200 | Search engine |
| **Kibana** | `5601` | http://localhost:5601 | Elasticsearch dashboard |

#### Wait for Services to be Ready
```bash
# Verify Kafka Connect REST API is ready to accept requests (returns empty array `[]` initially)
curl http://localhost:8083/connectors

# Check Elasticsearch health status (returns green/yellow state)
curl http://localhost:9200/_cluster/health
```

---

### Step 3: Register All Connectors
To capture changes from SQL Server and replicate them to PostgreSQL, register the source and sink connectors.

Open [kafka-connect.http]
inside an editor (such as VS Code with the **REST Client** extension installed). Execute all requests inside the file:
1. **Source Connectors**: Run POST requests to `/connectors` to create source connectors for `teams`, `players`, and `squad` tables.
2. **Sink Connectors**: Run POST requests to `/connectors` to create sink connectors to write Kafka events into PostgreSQL.

#### Alternative: Registering using cURL
If you don't use VS Code or the REST Client extension, you can execute the configuration registration commands via command line:

```bash
# Register Source Connectors (SQL Server -> Kafka)
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/source/teams-source-connector.json
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/source/players-source-connector.json
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/source/squad-source-connector.json

# Register Sink Connectors (Kafka -> PostgreSQL)
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/sink/teams-sink-connector.json
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/sink/players-sink-connector.json
curl -X POST http://localhost:8083/connectors -H "Content-Type: application/json" -d @connectors/sink/squad-sink-connector.json
```

#### Verify Connector Statuses
```bash
# List all registered connectors
curl http://localhost:8083/connectors

# Check status of specific connectors (should be "RUNNING")
curl http://localhost:8083/connectors/sqlserver-teams-source-connector/status
curl http://localhost:8083/connectors/postgres-teams-sink-connector/status
```

---

## Step 4: Run SearchService (Optional)

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

## Step 5: Verify CDC is Working

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

## Step 6: Test CDC with Data Changes

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
| CDC not enabled error | Run [script/init_sqlserver.sql](file:///c:/Users/NamDang/Documents/kafka-connect-debezium/script/init_sqlserver.sql) script first |
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