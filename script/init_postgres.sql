-- =============================================
-- PostgreSQL Script: Create Sport Database Tables
-- Target for Debezium Sink Connector
-- =============================================

-- =============================================
-- Table: teams
-- =============================================
CREATE TABLE IF NOT EXISTS teams (
    team_id INTEGER PRIMARY KEY,
    team_name VARCHAR(100) NOT NULL,
    city VARCHAR(100) NOT NULL,
    country VARCHAR(100) NOT NULL,
    founded_year INTEGER,
    stadium VARCHAR(150),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =============================================
-- Table: players
-- =============================================
CREATE TABLE IF NOT EXISTS players (
    player_id INTEGER PRIMARY KEY,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    date_of_birth DATE,
    nationality VARCHAR(100),
    position VARCHAR(50),
    jersey_number INTEGER,
    height_cm INTEGER,
    weight_kg INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =============================================
-- Table: squad
-- =============================================
CREATE TABLE IF NOT EXISTS squad (
    squad_id INTEGER PRIMARY KEY,
    team_id INTEGER NOT NULL,
    player_id INTEGER NOT NULL,
    join_date DATE NOT NULL,
    leave_date DATE,
    contract_value DECIMAL(15, 2),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_squad_team FOREIGN KEY (team_id) REFERENCES teams(team_id) ON DELETE CASCADE,
    CONSTRAINT fk_squad_player FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
);

-- =============================================
-- Create indexes for better performance
-- =============================================
CREATE INDEX IF NOT EXISTS idx_players_nationality ON players(nationality);
CREATE INDEX IF NOT EXISTS idx_players_position ON players(position);
CREATE INDEX IF NOT EXISTS idx_squad_team_id ON squad(team_id);
CREATE INDEX IF NOT EXISTS idx_squad_player_id ON squad(player_id);
CREATE INDEX IF NOT EXISTS idx_squad_is_active ON squad(is_active);

-- =============================================
-- Grant permissions (optional, if using different user)
-- =============================================
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO your_user;

SELECT 'PostgreSQL Sport database initialized successfully!' AS status;

