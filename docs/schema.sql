-- PrintIndustry Monitor - schema del database SQLite (App_Data/production.sqlite)
-- Esportato dal database in esecuzione il 20/11/2026. In esecuzione lo schema viene creato e
-- aggiornato automaticamente da DAL/Database.cs al primo avvio; questo file serve a documentarlo
-- e a ricrearlo a mano, ad esempio con: sqlite3 nuovo.sqlite < docs/schema.sql
-- Il database lavora in modalita WAL con le chiavi esterne attive (PRAGMA foreign_keys=ON).

PRAGMA foreign_keys = ON;

CREATE TABLE ImportFiles (
 MachineId INTEGER NOT NULL REFERENCES Machines(Id),
 OrigineDato TEXT NOT NULL,
 FileName TEXT NOT NULL,
 FileSize INTEGER NOT NULL,
 LastWriteTicks INTEGER NOT NULL,
 ImportedAt TEXT NOT NULL,
 PRIMARY KEY(MachineId, OrigineDato, FileName)
);

CREATE TABLE Imports (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 MachineId INTEGER NOT NULL REFERENCES Machines(Id),
 FileName TEXT NOT NULL,
 OrigineDato TEXT NOT NULL DEFAULT 'REALE',
 ImportedAt TEXT NOT NULL,
 Inserted INTEGER NOT NULL,
 Skipped INTEGER NOT NULL,
 ErrorText TEXT
);

CREATE TABLE Machines (
 Id INTEGER PRIMARY KEY,
 Code TEXT NOT NULL UNIQUE,
 Name TEXT NOT NULL,
 Format TEXT NOT NULL,
 HourCost NUMERIC NOT NULL DEFAULT 0,
 OperatorHourCost NUMERIC NOT NULL DEFAULT 0,
 PowerKw NUMERIC NOT NULL DEFAULT 0,
 EnergyEuroKwh NUMERIC NOT NULL DEFAULT 0,
 InkEuroLiter NUMERIC NOT NULL DEFAULT 0,
 InkCyanEuroLiter NUMERIC NOT NULL DEFAULT 0,
 InkMagentaEuroLiter NUMERIC NOT NULL DEFAULT 0,
 InkYellowEuroLiter NUMERIC NOT NULL DEFAULT 0,
 InkBlackEuroLiter NUMERIC NOT NULL DEFAULT 0,
 InkMlPerSqmEstimate NUMERIC NOT NULL DEFAULT 0,
 MediaEuroSqm NUMERIC NOT NULL DEFAULT 0,
 ToolEuroJob NUMERIC NOT NULL DEFAULT 0,
 AvailableHoursDay NUMERIC NOT NULL DEFAULT 8,
 Enabled INTEGER NOT NULL DEFAULT 1,
 Configured INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Production (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 MachineId INTEGER NOT NULL REFERENCES Machines(Id),
 SourceKey TEXT NOT NULL,
 OrigineDato TEXT NOT NULL DEFAULT 'REALE' CHECK (OrigineDato IN ('REALE','SIMULATO')),
 JobName TEXT,
 GroupKey TEXT,
 Status TEXT,
 StartedAt TEXT,
 FinishedAt TEXT,
 ActiveSeconds INTEGER,
 IdleSeconds INTEGER,
 Copies INTEGER,
 Rejected INTEGER,
 WidthMm NUMERIC,
 HeightMm NUMERIC,
 AreaSqm NUMERIC,
 InkMl NUMERIC,
 InkCyanMl NUMERIC,
 InkMagentaMl NUMERIC,
 InkYellowMl NUMERIC,
 InkBlackMl NUMERIC,
 Media TEXT,
 PrintMode TEXT,
 Successful INTEGER NOT NULL,
 AllocatedSeconds NUMERIC NOT NULL DEFAULT 0,
 CostCapturedAt TEXT,
 CostSnapshotSource TEXT,
 SnapHourCost NUMERIC,
 SnapOperatorHourCost NUMERIC,
 SnapPowerKw NUMERIC,
 SnapEnergyEuroKwh NUMERIC,
 SnapInkEuroLiter NUMERIC,
 SnapInkCyanEuroLiter NUMERIC,
 SnapInkMagentaEuroLiter NUMERIC,
 SnapInkYellowEuroLiter NUMERIC,
 SnapInkBlackEuroLiter NUMERIC,
 SnapInkMlPerSqmEstimate NUMERIC,
 SnapMediaEuroSqm NUMERIC,
 SnapToolEuroJob NUMERIC,
 CostMachine NUMERIC,
 CostOperator NUMERIC,
 CostEnergy NUMERIC,
 CostInk NUMERIC,
 CostInkCyan NUMERIC,
 CostInkMagenta NUMERIC,
 CostInkYellow NUMERIC,
 CostInkBlack NUMERIC,
 CostMedia NUMERIC,
 CostTool NUMERIC,
 CostIdleEstimate NUMERIC,
 EnergyKwh NUMERIC,
 CostTotal NUMERIC,
 UNIQUE(MachineId, OrigineDato, SourceKey)
);

CREATE INDEX IX_Production_Date ON Production(StartedAt);

CREATE INDEX IX_Production_MachineDate ON Production(MachineId, StartedAt);

CREATE INDEX IX_Production_MachineOrigineDate ON Production(MachineId, OrigineDato, StartedAt);

CREATE INDEX IX_Production_Status ON Production(Status);
