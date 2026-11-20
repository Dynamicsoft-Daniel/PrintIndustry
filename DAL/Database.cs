using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using PrintIndustry.Models;

namespace PrintIndustry.DAL
{
    public static class Db
    {
        private static readonly object InitLock = new object();
        private static volatile bool initialized = false;

        public static string Location
        {
            get
            {
                var path = AppDomain.CurrentDomain.GetData("DataDirectory") as string;

                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("App_Data non configurato.");
                }

                Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }
        }

        public static void ConfigureDataDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Percorso App_Data mancante.");
            }

            path = Path.GetFullPath(path);
            Directory.CreateDirectory(path);

            lock (InitLock)
            {
                AppDomain.CurrentDomain.SetData("DataDirectory", path);
                initialized = false;
            }
        }

        public static SQLiteConnection Open()
        {
            var path = Path.Combine(Location, "production.sqlite");
            var c = new SQLiteConnection("Data Source=" + path + ";Version=3;BusyTimeout=5000;");
            c.Open();

            // Non reimpostare journal_mode a ogni connessione: con molte richieste
            // provoca attese e lock inutili. WAL viene attivato una sola volta in Init().
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys=ON;", c))
            {
                cmd.ExecuteNonQuery();
            }

            return c;
        }

        public static void Init()
        {
            if (initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (initialized)
                {
                    return;
                }

                InitializeDatabase();
                initialized = true;
            }
        }

        private static void InitializeDatabase()
        {
            foreach (string machineFolder in new[] { "COLORADO_1650", "ZUND_G3" })
            {
                Directory.CreateDirectory(Path.Combine(Location, "Imports", machineFolder, "Reali"));
                Directory.CreateDirectory(Path.Combine(Location, "Imports", machineFolder, "Simulati"));
            }

            using (var c = Open())
            {
                // Impostazione persistente: eseguita solo all'avvio, non durante ogni file.
                using (var journal = new SQLiteCommand("PRAGMA journal_mode=WAL;", c))
                {
                    journal.ExecuteNonQuery();
                }

                var sql = @"
CREATE TABLE IF NOT EXISTS Machines (
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
CREATE TABLE IF NOT EXISTS Production (
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
CREATE TABLE IF NOT EXISTS Imports (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 MachineId INTEGER NOT NULL REFERENCES Machines(Id),
 FileName TEXT NOT NULL,
 OrigineDato TEXT NOT NULL DEFAULT 'REALE',
 ImportedAt TEXT NOT NULL,
 Inserted INTEGER NOT NULL,
 Skipped INTEGER NOT NULL,
 ErrorText TEXT
);
CREATE TABLE IF NOT EXISTS ImportFiles (
 MachineId INTEGER NOT NULL REFERENCES Machines(Id),
 OrigineDato TEXT NOT NULL,
 FileName TEXT NOT NULL,
 FileSize INTEGER NOT NULL,
 LastWriteTicks INTEGER NOT NULL,
 ImportedAt TEXT NOT NULL,
 PRIMARY KEY(MachineId, OrigineDato, FileName)
);
CREATE INDEX IF NOT EXISTS IX_Production_MachineDate ON Production(MachineId, StartedAt);
CREATE INDEX IF NOT EXISTS IX_Production_Date ON Production(StartedAt);";

                using (var cmd = new SQLiteCommand(sql, c))
                {
                    cmd.ExecuteNonQuery();
                }

                UpgradeSchema(c);
                EnsureColumns(c, "Machines", new[] { "ToolEuroJob" }, "NUMERIC NOT NULL DEFAULT 0");
                Seed(c, 1, "ZUND_G3", "Zünd G3 3XL-1600 - Taglio", "ZUND_XML");
                Seed(c, 2, "COLORADO", "Canon Colorado 1650", "COLORADO_CSV");
            }
        }

        // Raggruppamento per materiale e colore, filtrato come la tabella lavori.
        // Materiali: costo stimato usando la tariffa uniforme €/m² della macchina.
        public static object ColoradoConsumptions(int machineId, DateTime? from, DateTime? to,
            string status, string job, string origine = "REALE")
        {
            var items = new List<object>();
            decimal cyan = 0m;
            decimal magenta = 0m;
            decimal yellow = 0m;
            decimal black = 0m;
            decimal costoCyan = 0m;
            decimal costoMagenta = 0m;
            decimal costoYellow = 0m;
            decimal costoBlack = 0m;

            using (var c = Open())
            {
                var filter = @" FROM Production WHERE MachineId=@machine AND OrigineDato=@origine AND Successful=1
 AND (@from IS NULL OR StartedAt>=@from) AND (@to IS NULL OR StartedAt<@to)
 AND (@status IS NULL OR Status=@status) AND (@job IS NULL OR JobName LIKE @job ESCAPE '\')";

                using (var cmd = new SQLiteCommand(@"SELECT
 COALESCE(SUM(InkCyanMl),0), COALESCE(SUM(InkMagentaMl),0),
 COALESCE(SUM(InkYellowMl),0), COALESCE(SUM(InkBlackMl),0),
 COALESCE(SUM(CostInkCyan),0), COALESCE(SUM(CostInkMagenta),0),
 COALESCE(SUM(CostInkYellow),0), COALESCE(SUM(CostInkBlack),0)" + filter, c))
                {
                    AddConsumptionFilters(cmd, machineId, from, to, status, job, origine);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            cyan = Convert.ToDecimal(r.GetValue(0));
                            magenta = Convert.ToDecimal(r.GetValue(1));
                            yellow = Convert.ToDecimal(r.GetValue(2));
                            black = Convert.ToDecimal(r.GetValue(3));
                            costoCyan = Convert.ToDecimal(r.GetValue(4));
                            costoMagenta = Convert.ToDecimal(r.GetValue(5));
                            costoYellow = Convert.ToDecimal(r.GetValue(6));
                            costoBlack = Convert.ToDecimal(r.GetValue(7));
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(@"SELECT COALESCE(NULLIF(TRIM(Media),''),'Non specificato') AS Materiale,
 COALESCE(SUM(AreaSqm),0) AS Metri,
 COALESCE(SUM(CostMedia),0) AS Costo FROM Production WHERE MachineId=@machine AND OrigineDato=@origine AND Successful=1
 AND AreaSqm IS NOT NULL AND AreaSqm>0
 AND (@from IS NULL OR StartedAt>=@from) AND (@to IS NULL OR StartedAt<@to)
 AND (@status IS NULL OR Status=@status) AND (@job IS NULL OR JobName LIKE @job ESCAPE '\')
 GROUP BY COALESCE(NULLIF(TRIM(Media),''),'Non specificato') ORDER BY Metri DESC", c))
                {
                    AddConsumptionFilters(cmd, machineId, from, to, status, job, origine);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            items.Add(new
                            {
                                Name = r.GetString(0),
                                AreaSqm = Convert.ToDecimal(r.GetValue(1)),
                                Cost = Convert.ToDecimal(r.GetValue(2))
                            });
                        }
                    }
                }
            }

            return new
            {
                Inks = new[]
                {
                    new { Name = "Cyan", Ml = cyan, Cost = costoCyan },
                    new { Name = "Magenta", Ml = magenta, Cost = costoMagenta },
                    new { Name = "Yellow", Ml = yellow, Cost = costoYellow },
                    new { Name = "Black", Ml = black, Cost = costoBlack }
                },
                Materials = items
            };
        }

        private static void AddConsumptionFilters(SQLiteCommand cmd, int machineId, DateTime? from, DateTime? to,
            string status, string job, string origine)
        {
            cmd.Parameters.AddWithValue("@machine", machineId);
            cmd.Parameters.AddWithValue("@origine", origine);
            cmd.Parameters.AddWithValue("@from", from.HasValue
                ? (object)from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@to", to.HasValue
                ? (object)to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status);
            cmd.Parameters.AddWithValue("@job", JobTerm(job));
        }

        // Raggruppamento parametrico in SQLite: giorni, mesi o anni, con gli stessi filtri dei KPI.
        public static List<DailyProduction> PeriodTrend(int machineId, DateTime from, DateTime to, string grouping,
            string status = null, string job = null, string origine = "REALE")
        {
            var token = grouping == "year" ? "%Y" : grouping == "day" ? "%Y-%m-%d" : "%Y-%m";
            var list = new List<DailyProduction>();

            using (var c = Open())
            // Stessi criteri dei KPI: ore e area dei lavori riusciti, energia e costi di tutte le righe.
            using (var cmd = new SQLiteCommand(@"SELECT strftime(@pattern, StartedAt) AS Periodo,
 COUNT(*) AS Totale, SUM(CASE WHEN Successful=1 THEN 1 ELSE 0 END) AS Completate,
 COALESCE(SUM(CASE WHEN Successful=1 THEN AllocatedSeconds ELSE 0 END),0) / 3600.0 AS Ore,
 COALESCE(SUM(CASE WHEN Successful=1 THEN AreaSqm ELSE 0 END),0) AS Area,
 COALESCE(SUM(EnergyKwh),0) AS Energia,
 COALESCE(SUM(CostTotal),0) AS Costo
 FROM Production WHERE MachineId=@machine AND OrigineDato=@origine AND StartedAt IS NOT NULL
 AND StartedAt>=@from AND StartedAt<@to
 AND (@status IS NULL OR Status=@status)
 AND (@job IS NULL OR JobName LIKE @job ESCAPE '\')
 GROUP BY strftime(@pattern, StartedAt) ORDER BY Periodo", c))
            {
                cmd.Parameters.AddWithValue("@pattern", token);
                cmd.Parameters.AddWithValue("@machine", machineId);
                cmd.Parameters.AddWithValue("@origine", origine);
                cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status);
                cmd.Parameters.AddWithValue("@job", JobTerm(job));

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new DailyProduction
                        {
                            Day = r["Periodo"].ToString(),
                            Jobs = Convert.ToInt32(r["Totale"]),
                            GoodJobs = Convert.ToInt32(r["Completate"]),
                            Hours = Convert.ToDecimal(r["Ore"], CultureInfo.InvariantCulture),
                            AreaSqm = Convert.ToDecimal(r["Area"], CultureInfo.InvariantCulture),
                            EnergyKwh = Convert.ToDecimal(r["Energia"], CultureInfo.InvariantCulture),
                            Cost = Convert.ToDecimal(r["Costo"], CultureInfo.InvariantCulture)
                        });
                    }
                }
            }

            return list;
        }

        public static List<DailyProduction> DailyTrend(int machineId, DateTime? from, DateTime? to,
            string status = null, string job = null, string origine = "REALE")
        {
            // Stessi criteri dei KPI e dell'andamento adattivo: ore e area dei lavori riusciti,
            // energia e costi di tutte le righe. Senza estremi si considera tutto il periodo.
            return PeriodTrend(machineId, from ?? DateTime.MinValue, to ?? DateTime.MaxValue, "day",
                status, job, origine);
        }

        // Aggiornamento non distruttivo: il database e i dati esistenti sono conservati.
        private static void UpgradeSchema(SQLiteConnection c)
        {
            bool isNewColorColumns = !HasColumn(c, "Machines", "InkCyanEuroLiter");
            bool hasOldCosts = !HasColumn(c, "Production", "CostCapturedAt");

            EnsureColumns(c, "Machines",
                new[] { "InkCyanEuroLiter", "InkMagentaEuroLiter", "InkYellowEuroLiter", "InkBlackEuroLiter" },
                "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumns(c, "Production", new[] { "InkCyanMl", "InkMagentaMl", "InkYellowMl", "InkBlackMl" }, "NUMERIC");
            EnsureColumns(c, "Production", new[] { "FinishedAt" }, "TEXT");
            EnsureColumns(c, "Production", new[] { "OrigineDato" }, "TEXT NOT NULL DEFAULT 'REALE'");
            EnsureColumns(c, "Imports", new[] { "OrigineDato" }, "TEXT NOT NULL DEFAULT 'REALE'");

            // Compatibilità con database di versioni precedenti.
            EnsureColumns(c, "Production", new[] { "CostCapturedAt", "CostSnapshotSource" }, "TEXT");
            EnsureColumns(c, "Production", new[]
            {
                "SnapHourCost",
                "SnapOperatorHourCost",
                "SnapPowerKw",
                "SnapEnergyEuroKwh",
                "SnapInkEuroLiter",
                "SnapInkCyanEuroLiter",
                "SnapInkMagentaEuroLiter",
                "SnapInkYellowEuroLiter",
                "SnapInkBlackEuroLiter",
                "SnapInkMlPerSqmEstimate",
                "SnapMediaEuroSqm",
                "SnapToolEuroJob",
                "CostMachine",
                "CostOperator",
                "CostEnergy",
                "CostInk",
                "CostInkCyan",
                "CostInkMagenta",
                "CostInkYellow",
                "CostInkBlack",
                "CostMedia",
                "CostTool",
                "CostIdleEstimate",
                "EnergyKwh",
                "CostTotal"
            }, "NUMERIC");

            // Solo al passaggio al nuovo schema: non scandire le lavorazioni a ogni API.
            if (hasOldCosts)
            {
                BackfillOldCosts(c);
            }

            using (var cmd = new SQLiteCommand(
                "CREATE INDEX IF NOT EXISTS IX_Production_MachineOrigineDate ON Production(MachineId, OrigineDato, StartedAt)", c))
            {
                cmd.ExecuteNonQuery();
            }

            if (isNewColorColumns)
            {
                using (var cmd = new SQLiteCommand(@"UPDATE Machines SET
InkCyanEuroLiter=InkEuroLiter, InkMagentaEuroLiter=InkEuroLiter,
InkYellowEuroLiter=InkEuroLiter, InkBlackEuroLiter=InkEuroLiter
WHERE Code='COLORADO' AND InkCyanEuroLiter=0 AND InkMagentaEuroLiter=0 AND InkYellowEuroLiter=0 AND InkBlackEuroLiter=0", c))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Corregge solo vecchi valori iniziali non ancora configurati.
            // Non modifica prezzi personalizzati o esplicitamente impostati a zero.
            using (var cmd = new SQLiteCommand(@"UPDATE Machines SET
InkCyanEuroLiter=InkEuroLiter, InkMagentaEuroLiter=InkEuroLiter,
InkYellowEuroLiter=InkEuroLiter, InkBlackEuroLiter=InkEuroLiter
WHERE Code='COLORADO' AND Configured=0 AND InkEuroLiter>0
AND InkCyanEuroLiter=0 AND InkMagentaEuroLiter=0
AND InkYellowEuroLiter=0 AND InkBlackEuroLiter=0", c))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Production_Status ON Production(Status)", c))
            {
                cmd.ExecuteNonQuery();
            }
        }

        // Una tantum: per le righe pregresse prive di snapshot possiamo solo
        // congelare il listino disponibile alla migrazione, NON quello storico.
        // Non viene eseguito su record che possiedono già CostCapturedAt.
        private static void BackfillOldCosts(SQLiteConnection connection)
        {
            const string backfill = @"UPDATE Production SET
                SnapHourCost = (SELECT HourCost FROM Machines WHERE Id=Production.MachineId),
                SnapOperatorHourCost = (SELECT OperatorHourCost FROM Machines WHERE Id=Production.MachineId),
                SnapPowerKw = (SELECT PowerKw FROM Machines WHERE Id=Production.MachineId),
                SnapEnergyEuroKwh = (SELECT EnergyEuroKwh FROM Machines WHERE Id=Production.MachineId),
                SnapInkEuroLiter = (SELECT InkEuroLiter FROM Machines WHERE Id=Production.MachineId),
                SnapInkCyanEuroLiter = (SELECT InkCyanEuroLiter FROM Machines WHERE Id=Production.MachineId),
                SnapInkMagentaEuroLiter = (SELECT InkMagentaEuroLiter FROM Machines WHERE Id=Production.MachineId),
                SnapInkYellowEuroLiter = (SELECT InkYellowEuroLiter FROM Machines WHERE Id=Production.MachineId),
                SnapInkBlackEuroLiter = (SELECT InkBlackEuroLiter FROM Machines WHERE Id=Production.MachineId),
                SnapInkMlPerSqmEstimate = (SELECT InkMlPerSqmEstimate FROM Machines WHERE Id=Production.MachineId),
                SnapMediaEuroSqm = (SELECT MediaEuroSqm FROM Machines WHERE Id=Production.MachineId),
                SnapToolEuroJob = (SELECT ToolEuroJob FROM Machines WHERE Id=Production.MachineId),
                CostMachine = AllocatedSeconds / 3600.0 * (SELECT HourCost FROM Machines WHERE Id=Production.MachineId),
                CostOperator = AllocatedSeconds / 3600.0 * (SELECT OperatorHourCost FROM Machines WHERE Id=Production.MachineId),
                EnergyKwh = AllocatedSeconds / 3600.0 * (SELECT PowerKw FROM Machines WHERE Id=Production.MachineId),
                CostEnergy = AllocatedSeconds / 3600.0 * (SELECT PowerKw FROM Machines WHERE Id=Production.MachineId) * (SELECT EnergyEuroKwh FROM Machines WHERE Id=Production.MachineId),
                CostIdleEstimate = COALESCE(IdleSeconds,0) / 3600.0 * (SELECT HourCost FROM Machines WHERE Id=Production.MachineId),
                CostMedia = CASE WHEN Successful=1 THEN COALESCE(AreaSqm,0) * (SELECT MediaEuroSqm FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostTool = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='ZUND_G3') THEN (SELECT ToolEuroJob FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostInkCyan = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='COLORADO') THEN COALESCE(InkCyanMl,0) / 1000.0 * (SELECT InkCyanEuroLiter FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostInkMagenta = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='COLORADO') THEN COALESCE(InkMagentaMl,0) / 1000.0 * (SELECT InkMagentaEuroLiter FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostInkYellow = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='COLORADO') THEN COALESCE(InkYellowMl,0) / 1000.0 * (SELECT InkYellowEuroLiter FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostInkBlack = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='COLORADO') THEN COALESCE(InkBlackMl,0) / 1000.0 * (SELECT InkBlackEuroLiter FROM Machines WHERE Id=Production.MachineId) ELSE 0 END,
                CostInk = CASE WHEN Successful=1 AND MachineId=(SELECT Id FROM Machines WHERE Code='COLORADO') THEN CASE WHEN (InkCyanMl IS NOT NULL OR InkMagentaMl IS NOT NULL OR InkYellowMl IS NOT NULL OR InkBlackMl IS NOT NULL) THEN (COALESCE(InkCyanMl,0)*(SELECT InkCyanEuroLiter FROM Machines WHERE Id=Production.MachineId)+COALESCE(InkMagentaMl,0)*(SELECT InkMagentaEuroLiter FROM Machines WHERE Id=Production.MachineId)+COALESCE(InkYellowMl,0)*(SELECT InkYellowEuroLiter FROM Machines WHERE Id=Production.MachineId)+COALESCE(InkBlackMl,0)*(SELECT InkBlackEuroLiter FROM Machines WHERE Id=Production.MachineId)) / 1000.0 ELSE COALESCE(InkMl, CASE WHEN (SELECT InkMlPerSqmEstimate FROM Machines WHERE Id=Production.MachineId)>0 THEN AreaSqm*(SELECT InkMlPerSqmEstimate FROM Machines WHERE Id=Production.MachineId) ELSE NULL END,0) / 1000.0 * (SELECT InkEuroLiter FROM Machines WHERE Id=Production.MachineId) END ELSE 0 END
 WHERE CostCapturedAt IS NULL";

            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                using (SQLiteCommand command = new SQLiteCommand(backfill, connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                using (SQLiteCommand command = new SQLiteCommand(@"UPDATE Production SET
 CostTotal = COALESCE(CostMachine,0) + COALESCE(CostOperator,0) + COALESCE(CostEnergy,0) +
     COALESCE(CostInk,0) + COALESCE(CostMedia,0) + COALESCE(CostTool,0),
 CostCapturedAt = @date, CostSnapshotSource = 'MIGRAZIONE'
 WHERE CostCapturedAt IS NULL", connection, transaction))
                {
                    command.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        private static bool HasColumn(SQLiteConnection c, string table, string name)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", c))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    if (string.Equals(r["name"].ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureColumns(SQLiteConnection c, string table, IEnumerable<string> names, string type)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", c))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    existing.Add(r["name"].ToString());
                }
            }

            foreach (var name in names)
            {
                if (!existing.Contains(name))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE " + table + " ADD COLUMN " + name + " " + type, c))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // Valori iniziali per il caso di studio: costi indicativi, non prezzi aziendali certificati.
        // La Colorado 1650 assorbe circa 2,3 kW durante la stampa (scheda tecnica Canon).
        // Zünd G3: 3,6 kW come riferimento elettrico della macchina base, senza aspirazione.
        private static void Seed(SQLiteConnection c, int id, string code, string name, string format)
        {
            var isColorado = code == "COLORADO";

            using (var cmd = new SQLiteCommand(@"INSERT OR IGNORE INTO Machines
(Id, Code, Name, Format, HourCost, OperatorHourCost, PowerKw, EnergyEuroKwh,
 InkEuroLiter, InkCyanEuroLiter, InkMagentaEuroLiter, InkYellowEuroLiter, InkBlackEuroLiter, InkMlPerSqmEstimate, MediaEuroSqm, ToolEuroJob,
 AvailableHoursDay, Enabled, Configured)
VALUES (@id, @code, @name, @format, @hour, @operator, @power, @energy,
 @ink, @cyan, @magenta, @yellow, @black, 0, @media, @tool, 8, 1, 1)", c))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@format", format);
                cmd.Parameters.AddWithValue("@hour", isColorado ? 55m : 35m);
                cmd.Parameters.AddWithValue("@operator", 20m);
                cmd.Parameters.AddWithValue("@power", isColorado ? 2.3m : 3.6m);
                cmd.Parameters.AddWithValue("@energy", 0.25m);
                cmd.Parameters.AddWithValue("@ink", isColorado ? 120m : 0m);
                // Valori dimostrativi €/litro: l'utente può modificarli per colore.
                cmd.Parameters.AddWithValue("@cyan", isColorado ? 120m : 0m);
                cmd.Parameters.AddWithValue("@magenta", isColorado ? 120m : 0m);
                cmd.Parameters.AddWithValue("@yellow", isColorado ? 120m : 0m);
                cmd.Parameters.AddWithValue("@black", isColorado ? 120m : 0m);
                cmd.Parameters.AddWithValue("@media", 2.5m);
                cmd.Parameters.AddWithValue("@tool", isColorado ? 0m : 0.30m);
                cmd.ExecuteNonQuery();
            }
        }

        private static decimal D(SQLiteDataReader r, string name)
        {
            return Convert.ToDecimal(r[name], CultureInfo.InvariantCulture);
        }

        public static List<Machine> Machines()
        {
            Init();
            var list = new List<Machine>();

            using (var c = Open())
            using (var cmd = new SQLiteCommand("SELECT * FROM Machines ORDER BY Id", c))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Machine
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Code = r["Code"].ToString(),
                        Name = r["Name"].ToString(),
                        Format = r["Format"].ToString(),
                        HourCost = D(r, "HourCost"),
                        OperatorHourCost = D(r, "OperatorHourCost"),
                        PowerKw = D(r, "PowerKw"),
                        EnergyEuroKwh = D(r, "EnergyEuroKwh"),
                        InkEuroLiter = D(r, "InkEuroLiter"),
                        InkCyanEuroLiter = D(r, "InkCyanEuroLiter"),
                        InkMagentaEuroLiter = D(r, "InkMagentaEuroLiter"),
                        InkYellowEuroLiter = D(r, "InkYellowEuroLiter"),
                        InkBlackEuroLiter = D(r, "InkBlackEuroLiter"),
                        InkMlPerSqmEstimate = D(r, "InkMlPerSqmEstimate"),
                        MediaEuroSqm = D(r, "MediaEuroSqm"),
                        ToolEuroJob = D(r, "ToolEuroJob"),
                        AvailableHoursDay = D(r, "AvailableHoursDay"),
                        Enabled = Convert.ToInt32(r["Enabled"]) != 0,
                        Configured = Convert.ToInt32(r["Configured"]) != 0
                    });
                }
            }

            return list;
        }

        public static void SaveMachine(Machine m)
        {
            if (m == null || (m.Id != 1 && m.Id != 2))
            {
                throw new ArgumentException("Macchina non valida.");
            }

            var numbers = new[]
            {
                m.HourCost, m.OperatorHourCost, m.PowerKw, m.EnergyEuroKwh, m.InkEuroLiter,
                m.InkMlPerSqmEstimate, m.MediaEuroSqm, m.AvailableHoursDay,
                m.InkCyanEuroLiter, m.InkMagentaEuroLiter, m.InkYellowEuroLiter, m.InkBlackEuroLiter, m.ToolEuroJob
            };

            if (numbers.Any(x => x < 0))
            {
                throw new ArgumentException("I parametri non possono essere negativi.");
            }

            if (m.AvailableHoursDay <= 0 || m.AvailableHoursDay > 24)
            {
                throw new ArgumentException("Le ore disponibili al giorno devono essere maggiori di 0 e al massimo 24.");
            }

            // I parametri legati alla stampa non si applicano al plotter da taglio Zünd.
            // Azzerarli anche lato server evita valori obsoleti o richieste API errate.
            if (m.Id == 1)
            {
                m.InkEuroLiter = 0;
                m.InkCyanEuroLiter = 0;
                m.InkMagentaEuroLiter = 0;
                m.InkYellowEuroLiter = 0;
                m.InkBlackEuroLiter = 0;
                m.InkMlPerSqmEstimate = 0;
                // La Zünd impiega materiali e utensili: restano configurabili.
            }

            Init();

            using (var c = Open())
            using (var cmd = new SQLiteCommand(@"UPDATE Machines SET
HourCost=@hour,OperatorHourCost=@op,PowerKw=@power,EnergyEuroKwh=@energy,
InkEuroLiter=@ink,InkMlPerSqmEstimate=@estimate,MediaEuroSqm=@media,ToolEuroJob=@tool,
InkCyanEuroLiter=@cyan,InkMagentaEuroLiter=@magenta,InkYellowEuroLiter=@yellow,InkBlackEuroLiter=@black,
AvailableHoursDay=@hours,
Enabled=@enabled,Configured=1 WHERE Id=@id", c))
            {
                cmd.Parameters.AddWithValue("@id", m.Id);
                cmd.Parameters.AddWithValue("@hour", m.HourCost);
                cmd.Parameters.AddWithValue("@op", m.OperatorHourCost);
                cmd.Parameters.AddWithValue("@power", m.PowerKw);
                cmd.Parameters.AddWithValue("@energy", m.EnergyEuroKwh);
                cmd.Parameters.AddWithValue("@ink", m.InkEuroLiter);
                cmd.Parameters.AddWithValue("@cyan", m.InkCyanEuroLiter);
                cmd.Parameters.AddWithValue("@magenta", m.InkMagentaEuroLiter);
                cmd.Parameters.AddWithValue("@yellow", m.InkYellowEuroLiter);
                cmd.Parameters.AddWithValue("@black", m.InkBlackEuroLiter);
                cmd.Parameters.AddWithValue("@estimate", m.InkMlPerSqmEstimate);
                cmd.Parameters.AddWithValue("@media", m.MediaEuroSqm);
                cmd.Parameters.AddWithValue("@tool", m.ToolEuroJob);
                cmd.Parameters.AddWithValue("@hours", m.AvailableHoursDay);
                cmd.Parameters.AddWithValue("@enabled", m.Enabled ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        // Comando di inserimento riutilizzabile per un blocco di lavorazioni.
        // In questo modo SQLite non ricompila la stessa INSERT per ogni XML o riga CSV.
        private const string ProductionInsertSql = @"INSERT OR IGNORE INTO Production
(MachineId,SourceKey,OrigineDato,JobName,GroupKey,Status,StartedAt,FinishedAt,ActiveSeconds,IdleSeconds,
 Copies,Rejected,WidthMm,HeightMm,AreaSqm,InkMl,InkCyanMl,InkMagentaMl,InkYellowMl,InkBlackMl,Media,PrintMode,Successful,AllocatedSeconds,
 CostCapturedAt, CostSnapshotSource, SnapHourCost, SnapOperatorHourCost, SnapPowerKw, SnapEnergyEuroKwh, SnapInkEuroLiter, SnapInkCyanEuroLiter, SnapInkMagentaEuroLiter, SnapInkYellowEuroLiter, SnapInkBlackEuroLiter, SnapInkMlPerSqmEstimate, SnapMediaEuroSqm, SnapToolEuroJob, CostMachine, CostOperator, CostEnergy, CostInk, CostInkCyan, CostInkMagenta, CostInkYellow, CostInkBlack, CostMedia, CostTool, CostIdleEstimate, EnergyKwh, CostTotal)
VALUES(@machine,@key,@origine,@job,@group,@status,@date,@finish,@active,@idle,
 @copies,@rejected,@width,@height,@area,@ink,@cyanml,@magentaml,@yellowml,@blackml,@media,@mode,@success,@allocated,
 @snapCostCapturedAt, @snapCostSnapshotSource, @snapSnapHourCost, @snapSnapOperatorHourCost, @snapSnapPowerKw, @snapSnapEnergyEuroKwh, @snapSnapInkEuroLiter, @snapSnapInkCyanEuroLiter, @snapSnapInkMagentaEuroLiter, @snapSnapInkYellowEuroLiter, @snapSnapInkBlackEuroLiter, @snapSnapInkMlPerSqmEstimate, @snapSnapMediaEuroSqm, @snapSnapToolEuroJob, @snapCostMachine, @snapCostOperator, @snapCostEnergy, @snapCostInk, @snapCostInkCyan, @snapCostInkMagenta, @snapCostInkYellow, @snapCostInkBlack, @snapCostMedia, @snapCostTool, @snapCostIdleEstimate, @snapEnergyKwh, @snapCostTotal)";

        public static SQLiteCommand CreateProductionInsertCommand(SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            SQLiteCommand result = new SQLiteCommand(ProductionInsertSql, connection, transaction);

            try
            {
                result.Parameters.AddWithValue("@machine", DBNull.Value);
                result.Parameters.AddWithValue("@key", DBNull.Value);
                result.Parameters.AddWithValue("@origine", DBNull.Value);
                result.Parameters.AddWithValue("@job", DBNull.Value);
                result.Parameters.AddWithValue("@group", DBNull.Value);
                result.Parameters.AddWithValue("@status", DBNull.Value);
                result.Parameters.AddWithValue("@date", DBNull.Value);
                result.Parameters.AddWithValue("@finish", DBNull.Value);
                result.Parameters.AddWithValue("@active", DBNull.Value);
                result.Parameters.AddWithValue("@idle", DBNull.Value);
                result.Parameters.AddWithValue("@copies", DBNull.Value);
                result.Parameters.AddWithValue("@rejected", DBNull.Value);
                result.Parameters.AddWithValue("@width", DBNull.Value);
                result.Parameters.AddWithValue("@height", DBNull.Value);
                result.Parameters.AddWithValue("@area", DBNull.Value);
                result.Parameters.AddWithValue("@ink", DBNull.Value);
                result.Parameters.AddWithValue("@cyanml", DBNull.Value);
                result.Parameters.AddWithValue("@magentaml", DBNull.Value);
                result.Parameters.AddWithValue("@yellowml", DBNull.Value);
                result.Parameters.AddWithValue("@blackml", DBNull.Value);
                result.Parameters.AddWithValue("@media", DBNull.Value);
                result.Parameters.AddWithValue("@mode", DBNull.Value);
                result.Parameters.AddWithValue("@success", DBNull.Value);
                result.Parameters.AddWithValue("@allocated", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostCapturedAt", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostSnapshotSource", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapHourCost", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapOperatorHourCost", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapPowerKw", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapEnergyEuroKwh", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkEuroLiter", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkCyanEuroLiter", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkMagentaEuroLiter", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkYellowEuroLiter", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkBlackEuroLiter", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapInkMlPerSqmEstimate", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapMediaEuroSqm", DBNull.Value);
                result.Parameters.AddWithValue("@snapSnapToolEuroJob", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostMachine", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostOperator", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostEnergy", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostInk", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostInkCyan", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostInkMagenta", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostInkYellow", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostInkBlack", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostMedia", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostTool", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostIdleEstimate", DBNull.Value);
                result.Parameters.AddWithValue("@snapEnergyKwh", DBNull.Value);
                result.Parameters.AddWithValue("@snapCostTotal", DBNull.Value);

                result.Prepare();
            }
            catch (Exception)
            {
                result.Dispose();
                throw;
            }

            return result;
        }

        public static bool Insert(SQLiteCommand command, ProductionData item)
        {
            command.Parameters["@machine"].Value = item.MachineId;
            command.Parameters["@key"].Value = item.SourceKey;
            command.Parameters["@origine"].Value = item.OrigineDato;
            command.Parameters["@job"].Value = (object)item.JobName ?? DBNull.Value;
            command.Parameters["@group"].Value = (object)item.GroupKey ?? DBNull.Value;
            command.Parameters["@status"].Value = (object)item.Status ?? DBNull.Value;
            command.Parameters["@date"].Value = item.StartedAt.HasValue
                ? (object)item.StartedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value;
            command.Parameters["@finish"].Value = item.FinishedAt.HasValue
                ? (object)item.FinishedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value;
            command.Parameters["@active"].Value = (object)item.ActiveSeconds ?? DBNull.Value;
            command.Parameters["@idle"].Value = (object)item.IdleSeconds ?? DBNull.Value;
            command.Parameters["@copies"].Value = (object)item.Copies ?? DBNull.Value;
            command.Parameters["@rejected"].Value = (object)item.Rejected ?? DBNull.Value;
            command.Parameters["@width"].Value = (object)item.WidthMm ?? DBNull.Value;
            command.Parameters["@height"].Value = (object)item.HeightMm ?? DBNull.Value;
            command.Parameters["@area"].Value = (object)item.AreaSqm ?? DBNull.Value;
            command.Parameters["@ink"].Value = (object)item.InkMl ?? DBNull.Value;
            command.Parameters["@cyanml"].Value = (object)item.InkCyanMl ?? DBNull.Value;
            command.Parameters["@magentaml"].Value = (object)item.InkMagentaMl ?? DBNull.Value;
            command.Parameters["@yellowml"].Value = (object)item.InkYellowMl ?? DBNull.Value;
            command.Parameters["@blackml"].Value = (object)item.InkBlackMl ?? DBNull.Value;
            command.Parameters["@media"].Value = (object)item.Media ?? DBNull.Value;
            command.Parameters["@mode"].Value = (object)item.PrintMode ?? DBNull.Value;
            command.Parameters["@success"].Value = item.Successful ? 1 : 0;
            command.Parameters["@allocated"].Value = item.AllocatedSeconds;
            command.Parameters["@snapCostCapturedAt"].Value = item.CostCapturedAt.HasValue ?
                    (object)item.CostCapturedAt.Value.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value;
            command.Parameters["@snapCostSnapshotSource"].Value = (object)item.CostSnapshotSource ?? DBNull.Value;
            command.Parameters["@snapSnapHourCost"].Value = item.SnapHourCost;
            command.Parameters["@snapSnapOperatorHourCost"].Value = item.SnapOperatorHourCost;
            command.Parameters["@snapSnapPowerKw"].Value = item.SnapPowerKw;
            command.Parameters["@snapSnapEnergyEuroKwh"].Value = item.SnapEnergyEuroKwh;
            command.Parameters["@snapSnapInkEuroLiter"].Value = item.SnapInkEuroLiter;
            command.Parameters["@snapSnapInkCyanEuroLiter"].Value = item.SnapInkCyanEuroLiter;
            command.Parameters["@snapSnapInkMagentaEuroLiter"].Value = item.SnapInkMagentaEuroLiter;
            command.Parameters["@snapSnapInkYellowEuroLiter"].Value = item.SnapInkYellowEuroLiter;
            command.Parameters["@snapSnapInkBlackEuroLiter"].Value = item.SnapInkBlackEuroLiter;
            command.Parameters["@snapSnapInkMlPerSqmEstimate"].Value = item.SnapInkMlPerSqmEstimate;
            command.Parameters["@snapSnapMediaEuroSqm"].Value = item.SnapMediaEuroSqm;
            command.Parameters["@snapSnapToolEuroJob"].Value = item.SnapToolEuroJob;
            command.Parameters["@snapCostMachine"].Value = item.CostMachine;
            command.Parameters["@snapCostOperator"].Value = item.CostOperator;
            command.Parameters["@snapCostEnergy"].Value = item.CostEnergy;
            command.Parameters["@snapCostInk"].Value = item.CostInk;
            command.Parameters["@snapCostInkCyan"].Value = item.CostInkCyan;
            command.Parameters["@snapCostInkMagenta"].Value = item.CostInkMagenta;
            command.Parameters["@snapCostInkYellow"].Value = item.CostInkYellow;
            command.Parameters["@snapCostInkBlack"].Value = item.CostInkBlack;
            command.Parameters["@snapCostMedia"].Value = item.CostMedia;
            command.Parameters["@snapCostTool"].Value = item.CostTool;
            command.Parameters["@snapCostIdleEstimate"].Value = item.CostIdleEstimate;
            command.Parameters["@snapEnergyKwh"].Value = item.EnergyKwh;
            command.Parameters["@snapCostTotal"].Value = item.CostTotal;

            return command.ExecuteNonQuery() == 1;
        }

        public class ImportedFileStamp
        {
            public long Length { get; set; }
            public long ModifiedTicks { get; set; }
        }

        // Una sola SELECT per macchina: altrimenti 7.000 XML causano 7.000
        // interrogazioni solo per verificare che il file sia già stato acquisito.
        public static Dictionary<string, ImportedFileStamp> ImportedFileStamps(
            SQLiteConnection connection, int machineId, string origine)
        {
            Dictionary<string, ImportedFileStamp> result =
                new Dictionary<string, ImportedFileStamp>(StringComparer.Ordinal);

            try
            {
                using (SQLiteCommand command = new SQLiteCommand(@"SELECT FileName, FileSize, LastWriteTicks
FROM ImportFiles WHERE MachineId=@machine AND OrigineDato=@origine", connection))
                {
                    command.Parameters.AddWithValue("@machine", machineId);
                    command.Parameters.AddWithValue("@origine", origine);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ImportedFileStamp stamp = new ImportedFileStamp();
                            stamp.Length = Convert.ToInt64(reader["FileSize"]);
                            stamp.ModifiedTicks = Convert.ToInt64(reader["LastWriteTicks"]);
                            result[reader["FileName"].ToString()] = stamp;
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        public static bool IsFileUnchanged(Dictionary<string, ImportedFileStamp> stamps, FileInfo file)
        {
            ImportedFileStamp stamp;
            bool result = stamps.TryGetValue(file.Name, out stamp) &&
                stamp.Length == file.Length && stamp.ModifiedTicks == file.LastWriteTimeUtc.Ticks;

            return result;
        }

        // I tre comandi SQL vengono preparati una sola volta per blocco.
        public static SQLiteCommand CreateMarkFileCommand(SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            SQLiteCommand result = new SQLiteCommand(@"INSERT OR REPLACE INTO ImportFiles
(MachineId,OrigineDato,FileName,FileSize,LastWriteTicks,ImportedAt)
VALUES(@machine,@origine,@name,@size,@modified,@date)", connection, transaction);

            result.Parameters.AddWithValue("@machine", 0);
            result.Parameters.AddWithValue("@origine", "");
            result.Parameters.AddWithValue("@name", "");
            result.Parameters.AddWithValue("@size", 0L);
            result.Parameters.AddWithValue("@modified", 0L);
            result.Parameters.AddWithValue("@date", "");
            result.Prepare();

            return result;
        }

        public static void MarkFileImported(SQLiteCommand command,
            int machineId, string origine, FileInfo file)
        {
            command.Parameters["@machine"].Value = machineId;
            command.Parameters["@origine"].Value = origine;
            command.Parameters["@name"].Value = file.Name;
            command.Parameters["@size"].Value = file.Length;
            command.Parameters["@modified"].Value = file.LastWriteTimeUtc.Ticks;
            command.Parameters["@date"].Value = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            command.ExecuteNonQuery();
        }

        public static SQLiteCommand CreateImportLogCommand(SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            SQLiteCommand result = new SQLiteCommand(@"INSERT INTO Imports
(MachineId,FileName,OrigineDato,ImportedAt,Inserted,Skipped,ErrorText)
VALUES(@machine,@file,@origine,@date,@inserted,@skipped,@error)", connection, transaction);

            result.Parameters.AddWithValue("@machine", 0);
            result.Parameters.AddWithValue("@file", "");
            result.Parameters.AddWithValue("@origine", "");
            result.Parameters.AddWithValue("@date", "");
            result.Parameters.AddWithValue("@inserted", 0);
            result.Parameters.AddWithValue("@skipped", 0);
            result.Parameters.AddWithValue("@error", DBNull.Value);
            result.Prepare();

            return result;
        }

        public static void Log(SQLiteCommand command, int machineId, string file,
            int inserted, int skipped, string error, string origine)
        {
            command.Parameters["@machine"].Value = machineId;
            command.Parameters["@file"].Value = Path.GetFileName(file);
            command.Parameters["@origine"].Value = origine;
            command.Parameters["@date"].Value = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            command.Parameters["@inserted"].Value = inserted;
            command.Parameters["@skipped"].Value = skipped;
            command.Parameters["@error"].Value = (object)error ?? DBNull.Value;
            command.ExecuteNonQuery();
        }

        public static List<ProductionData> Productions(int? machine, int limit = 3000, DateTime? from = null,
            DateTime? to = null, string status = null, int offset = 0, string job = null, string origine = "REALE")
        {
            Init();
            var result = new List<ProductionData>();

            using (var c = Open())
            using (var cmd = new SQLiteCommand(@"SELECT * FROM Production
WHERE MachineId=@machine
  AND OrigineDato=@origine
  AND (@from IS NULL OR StartedAt>=@from)
  AND (@to IS NULL OR StartedAt<@to)
  AND (@status IS NULL OR Status=@status)
  AND (@job IS NULL OR JobName LIKE @job ESCAPE '\')
ORDER BY StartedAt DESC,Id DESC LIMIT @limit OFFSET @offset", c))
            {
                cmd.Parameters.AddWithValue("@machine", machine.HasValue ? (object)machine.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@origine", origine);
                cmd.Parameters.AddWithValue("@limit", Math.Min(Math.Max(limit, 1), 100000));
                cmd.Parameters.AddWithValue("@offset", Math.Max(0, offset));
                cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status);
                cmd.Parameters.AddWithValue("@job", JobTerm(job));
                cmd.Parameters.AddWithValue("@from", from.HasValue
                    ? (object)from.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("@to", to.HasValue
                    ? (object)to.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        result.Add(new ProductionData
                        {
                            Id = Convert.ToInt64(r["Id"]),
                            MachineId = Convert.ToInt32(r["MachineId"]),
                            SourceKey = r["SourceKey"].ToString(),
                            OrigineDato = S(r, "OrigineDato"),
                            JobName = S(r, "JobName"),
                            GroupKey = S(r, "GroupKey"),
                            Status = S(r, "Status"),
                            StartedAt = Date(r, "StartedAt"),
                            FinishedAt = Date(r, "FinishedAt"),
                            ActiveSeconds = I(r, "ActiveSeconds"),
                            IdleSeconds = I(r, "IdleSeconds"),
                            Copies = I(r, "Copies"),
                            Rejected = I(r, "Rejected"),
                            WidthMm = N(r, "WidthMm"),
                            HeightMm = N(r, "HeightMm"),
                            AreaSqm = N(r, "AreaSqm"),
                            InkMl = N(r, "InkMl"),
                            InkCyanMl = N(r, "InkCyanMl"),
                            InkMagentaMl = N(r, "InkMagentaMl"),
                            InkYellowMl = N(r, "InkYellowMl"),
                            InkBlackMl = N(r, "InkBlackMl"),
                            Media = S(r, "Media"),
                            PrintMode = S(r, "PrintMode"),
                            Successful = Convert.ToInt32(r["Successful"]) == 1,
                            AllocatedSeconds = D(r, "AllocatedSeconds"),
                            CostCapturedAt = Date(r, "CostCapturedAt"),
                            CostSnapshotSource = S(r, "CostSnapshotSource"),
                            SnapHourCost = r["SnapHourCost"] == DBNull.Value ? 0m : D(r, "SnapHourCost"),
                            SnapOperatorHourCost = r["SnapOperatorHourCost"] == DBNull.Value ? 0m : D(r, "SnapOperatorHourCost"),
                            SnapPowerKw = r["SnapPowerKw"] == DBNull.Value ? 0m : D(r, "SnapPowerKw"),
                            SnapEnergyEuroKwh = r["SnapEnergyEuroKwh"] == DBNull.Value ? 0m : D(r, "SnapEnergyEuroKwh"),
                            SnapInkEuroLiter = r["SnapInkEuroLiter"] == DBNull.Value ? 0m : D(r, "SnapInkEuroLiter"),
                            SnapInkCyanEuroLiter = r["SnapInkCyanEuroLiter"] == DBNull.Value ? 0m : D(r, "SnapInkCyanEuroLiter"),
                            SnapInkMagentaEuroLiter = r["SnapInkMagentaEuroLiter"] == DBNull.Value ? 0m : D(r, "SnapInkMagentaEuroLiter"),
                            SnapInkYellowEuroLiter = r["SnapInkYellowEuroLiter"] == DBNull.Value ? 0m : D(r, "SnapInkYellowEuroLiter"),
                            SnapInkBlackEuroLiter = r["SnapInkBlackEuroLiter"] == DBNull.Value ? 0m : D(r, "SnapInkBlackEuroLiter"),
                            SnapInkMlPerSqmEstimate = r["SnapInkMlPerSqmEstimate"] == DBNull.Value ? 0m : D(r, "SnapInkMlPerSqmEstimate"),
                            SnapMediaEuroSqm = r["SnapMediaEuroSqm"] == DBNull.Value ? 0m : D(r, "SnapMediaEuroSqm"),
                            SnapToolEuroJob = r["SnapToolEuroJob"] == DBNull.Value ? 0m : D(r, "SnapToolEuroJob"),
                            CostMachine = r["CostMachine"] == DBNull.Value ? 0m : D(r, "CostMachine"),
                            CostOperator = r["CostOperator"] == DBNull.Value ? 0m : D(r, "CostOperator"),
                            CostEnergy = r["CostEnergy"] == DBNull.Value ? 0m : D(r, "CostEnergy"),
                            CostInk = r["CostInk"] == DBNull.Value ? 0m : D(r, "CostInk"),
                            CostInkCyan = r["CostInkCyan"] == DBNull.Value ? 0m : D(r, "CostInkCyan"),
                            CostInkMagenta = r["CostInkMagenta"] == DBNull.Value ? 0m : D(r, "CostInkMagenta"),
                            CostInkYellow = r["CostInkYellow"] == DBNull.Value ? 0m : D(r, "CostInkYellow"),
                            CostInkBlack = r["CostInkBlack"] == DBNull.Value ? 0m : D(r, "CostInkBlack"),
                            CostMedia = r["CostMedia"] == DBNull.Value ? 0m : D(r, "CostMedia"),
                            CostTool = r["CostTool"] == DBNull.Value ? 0m : D(r, "CostTool"),
                            CostIdleEstimate = r["CostIdleEstimate"] == DBNull.Value ? 0m : D(r, "CostIdleEstimate"),
                            EnergyKwh = r["EnergyKwh"] == DBNull.Value ? 0m : D(r, "EnergyKwh"),
                            CostTotal = r["CostTotal"] == DBNull.Value ? 0m : D(r, "CostTotal")
                        });
                    }
                }
            }

            return result;
        }

        public static int ProductionCount(int? machine, DateTime? from, DateTime? to, string status,
            string job = null, string origine = "REALE")
        {
            Init();

            using (var c = Open())
            using (var cmd = new SQLiteCommand(@"SELECT COUNT(*) FROM Production WHERE
MachineId=@machine AND OrigineDato=@origine AND (@from IS NULL OR StartedAt>=@from)
AND (@to IS NULL OR StartedAt<@to) AND (@status IS NULL OR Status=@status)
AND (@job IS NULL OR JobName LIKE @job ESCAPE '\')", c))
            {
                cmd.Parameters.AddWithValue("@machine", machine.HasValue ? (object)machine.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@origine", origine);
                cmd.Parameters.AddWithValue("@from", from.HasValue
                    ? (object)from.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("@to", to.HasValue
                    ? (object)to.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status);
                cmd.Parameters.AddWithValue("@job", JobTerm(job));

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static List<string> ProductionStatuses(int? machine, string origine = "REALE")
        {
            Init();
            var result = new List<string>();

            using (var c = Open())
            using (var cmd = new SQLiteCommand(@"SELECT DISTINCT Status FROM Production
WHERE Status IS NOT NULL AND TRIM(Status)<>'' AND OrigineDato=@origine AND MachineId=@machine
ORDER BY Status", c))
            {
                cmd.Parameters.AddWithValue("@machine", machine.HasValue ? (object)machine.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@origine", origine);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        result.Add(r.GetString(0));
                    }
                }
            }

            return result;
        }

        public static OriginiMacchina ContaOrigini(int machineId)
        {
            OriginiMacchina result = new OriginiMacchina();

            Init();

            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = new SQLiteCommand(@"SELECT OrigineDato, COUNT(*) AS Totale
FROM Production
WHERE MachineId = @machine
GROUP BY OrigineDato", connection))
            {
                command.Parameters.AddWithValue("@machine", machineId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["OrigineDato"].ToString() == "REALE")
                        {
                            result.Reali = Convert.ToInt32(reader["Totale"]);
                        }
                        else if (reader["OrigineDato"].ToString() == "SIMULATO")
                        {
                            result.Simulati = Convert.ToInt32(reader["Totale"]);
                        }
                    }
                }
            }

            return result;
        }

        private static object JobTerm(string job)
        {
            if (string.IsNullOrWhiteSpace(job))
            {
                return DBNull.Value;
            }

            return "%" + job.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        }

        private static string S(SQLiteDataReader r, string name)
        {
            return r[name] == DBNull.Value ? null : r[name].ToString();
        }

        private static int? I(SQLiteDataReader r, string name)
        {
            return r[name] == DBNull.Value ? (int?)null : Convert.ToInt32(r[name]);
        }

        private static decimal? N(SQLiteDataReader r, string name)
        {
            return r[name] == DBNull.Value ? (decimal?)null : D(r, name);
        }

        private static DateTime? Date(SQLiteDataReader r, string name)
        {
            DateTime value;
            return DateTime.TryParse(S(r, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                ? value : (DateTime?)null;
        }
    }
}
