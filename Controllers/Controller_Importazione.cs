using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using PrintIndustry.DAL;
using PrintIndustry.Models;

namespace PrintIndustry.Controllers
{
    public static class Controller_Importazione
    {
        private const int FilesPerBatch = 300;
        private static readonly object ImportLock = new object();

        private static IProductionParser GetParser(Machine machine)
        {
            IProductionParser result = null;

            if (machine == null || !machine.Enabled || !machine.Configured)
            {
                throw new ArgumentException("Macchina non configurata o disabilitata.");
            }

            if (machine.Format == "ZUND_XML")
            {
                result = new ZundG3Parser();
            }
            else if (machine.Format == "COLORADO_CSV")
            {
                result = new ColoradoParser();
            }
            else
            {
                throw new InvalidDataException("Formato di importazione sconosciuto.");
            }

            return result;
        }

        private static List<ProductionData> ReadProductionFile(Machine machine, string file,
            bool simulated, IProductionParser parser, Controller_Costi controllerCosti)
        {
            List<ProductionData> result = new List<ProductionData>();

            try
            {
                result = parser.Parse(file, machine).ToList();

                foreach (ProductionData record in result)
                {
                    record.AllocatedSeconds = record.ActiveSeconds ?? 0;
                    record.OrigineDato = simulated ? "SIMULATO" : "REALE";
                    controllerCosti.Calcola(record, machine);

                    if (simulated)
                    {
                        record.SourceKey = "SIMULATO:" + record.SourceKey;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        private static ImportResult ImportFile(Machine machine, string file, bool simulated,
            FileInfo info, IProductionParser parser, Controller_Costi controllerCosti,
            SQLiteCommand insertCommand, SQLiteCommand markCommand, SQLiteCommand logCommand)
        {
            ImportResult result = new ImportResult();
            int inserted = 0;
            int skipped = 0;
            string origine = simulated ? "SIMULATO" : "REALE";

            try
            {
                List<ProductionData> records = ReadProductionFile(machine, file, simulated,
                    parser, controllerCosti);

                foreach (ProductionData record in records)
                {
                    if (Db.Insert(insertCommand, record))
                    {
                        inserted++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                Db.Log(logCommand, machine.Id, file, inserted, skipped, null, origine);
                Db.MarkFileImported(markCommand, machine.Id, origine, info);

                result.Machine = machine.Name;
                result.Inserted = inserted;
                result.Skipped = skipped;
                result.Message = info.Name;
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        private static void ExecuteSavepoint(SQLiteCommand command, string statement)
        {
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }

        private static void AddFileError(ImportResult summary, string file, string message)
        {
            summary.Errors++;

            if (summary.Errors <= 3)
            {
                if (!string.IsNullOrEmpty(summary.Message))
                {
                    summary.Message += "\n";
                }

                // Non ripetere nel popup il percorso completo di OneDrive.
                summary.Message += Path.GetFileName(file) + ": " + message;
            }
        }

        private static string DescrizioneErroreFile(Exception ex)
        {
            string result = ex.Message;

            if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                result = "File non raggiungibile. Controllare lunghezza del percorso, " +
                    "sincronizzazione OneDrive e presenza del file sul disco.";
            }
            else if (ex is PathTooLongException)
            {
                result = "Percorso troppo lungo. Spostare il progetto in una cartella " +
                    "più breve, per esempio C:\\PrintIndustry.";
            }
            else if (ex is UnauthorizedAccessException)
            {
                result = "Accesso negato al file. Controllare i permessi della cartella.";
            }

            return result;
        }

        public static List<ImportResult> ImportFolder(int? machineId = null, bool simulated = false)
        {
            List<ImportResult> result = new List<ImportResult>();

            if (!Monitor.TryEnter(ImportLock))
            {
                throw new ArgumentException("Un'altra importazione è già in corso.");
            }

            try
            {
                result = ImportFolderCore(machineId, simulated);
            }
            finally
            {
                Monitor.Exit(ImportLock);
            }

            return result;
        }

        private static List<ImportResult> ImportFolderCore(int? machineId, bool simulated)
        {
            List<ImportResult> result = new List<ImportResult>();
            string origine = simulated ? "SIMULATO" : "REALE";
            Db.Init();

            List<Machine> machines = Db.Machines()
                .Where(x => x.Enabled && (!machineId.HasValue || x.Id == machineId.Value))
                .ToList();

            foreach (Machine machine in machines)
            {
                ImportResult summary = new ImportResult();
                summary.Machine = machine.Name;
                Stopwatch stopwatch = Stopwatch.StartNew();

                string machineFolder = machine.Code == "ZUND_G3" ? "ZUND_G3" : "COLORADO_1650";
                string sourceFolder = simulated ? "Simulati" : "Reali";
                string folder = Path.Combine(Db.Location, "Imports", machineFolder, sourceFolder);
                string extension = machine.Format == "COLORADO_CSV" ? ".csv" : ".xml";

                Directory.CreateDirectory(folder);

                IProductionParser parser = GetParser(machine);
                Controller_Costi controllerCosti = new Controller_Costi();

                IEnumerable<string> files = Directory.EnumerateFiles(folder)
                    .Where(x => string.Equals(Path.GetExtension(x), extension,
                        StringComparison.OrdinalIgnoreCase));

                using (SQLiteConnection connection = Db.Open())
                {
                    // Un'unica lettura del registro dei file, anche con 7.000 XML.
                    Dictionary<string, Db.ImportedFileStamp> stamps =
                        Db.ImportedFileStamps(connection, machine.Id, origine);

                    SQLiteTransaction transaction = null;
                    SQLiteCommand insertCommand = null;
                    SQLiteCommand markCommand = null;
                    SQLiteCommand logCommand = null;
                    SQLiteCommand savepointCommand = null;
                    int processed = 0;

                    try
                    {
                        foreach (string file in files)
                        {
                            summary.CheckedFiles++;
                            FileInfo info = null;
                            bool fileUnchanged = false;

                            try
                            {
                                info = Controller_Percorsi.InformazioniFile(file);
                                fileUnchanged = Db.IsFileUnchanged(stamps, info);
                            }
                            catch (Exception ex)
                            {
                                // Il file potrebbe essere stato spostato o eliminato
                                // mentre si stava scorrendo la cartella.
                                AddFileError(summary, file, DescrizioneErroreFile(ex));
                                continue;
                            }

                            // I file invariati non avviano nemmeno una transazione.
                            if (fileUnchanged)
                            {
                                summary.UnchangedFiles++;
                                summary.Skipped++;
                                continue;
                            }

                            if (transaction == null)
                            {
                                transaction = connection.BeginTransaction();
                                insertCommand = Db.CreateProductionInsertCommand(connection, transaction);
                                markCommand = Db.CreateMarkFileCommand(connection, transaction);
                                logCommand = Db.CreateImportLogCommand(connection, transaction);
                                savepointCommand = new SQLiteCommand(string.Empty, connection, transaction);
                            }

                            try
                            {
                                ExecuteSavepoint(savepointCommand, "SAVEPOINT file_import");

                                try
                                {
                                    ImportResult imported = ImportFile(machine, file, simulated, info,
                                        parser, controllerCosti, insertCommand, markCommand, logCommand);

                                    ExecuteSavepoint(savepointCommand, "RELEASE SAVEPOINT file_import");

                                    summary.Inserted += imported.Inserted;
                                    summary.Skipped += imported.Skipped;

                                    if (imported.Inserted > 0)
                                    {
                                        summary.NewFiles++;
                                    }
                                    else
                                    {
                                        // File letto per la prima volta ma senza lavorazioni nuove:
                                        // copia di un file già importato oppure file vuoto.
                                        summary.FilesWithoutNewData++;
                                    }

                                    // Il file è stato salvato: aggiorna anche la cache locale.
                                    Db.ImportedFileStamp stamp = new Db.ImportedFileStamp();
                                    stamp.Length = info.Length;
                                    stamp.ModifiedTicks = info.LastWriteTimeUtc.Ticks;
                                    stamps[info.Name] = stamp;
                                }
                                catch (Exception)
                                {
                                    ExecuteSavepoint(savepointCommand, "ROLLBACK TO SAVEPOINT file_import");
                                    ExecuteSavepoint(savepointCommand, "RELEASE SAVEPOINT file_import");
                                    throw;
                                }
                            }
                            catch (Exception ex)
                            {
                                AddFileError(summary, file, DescrizioneErroreFile(ex));

                                // L'errore di un file non blocca gli altri.
                                Db.Log(logCommand, machine.Id, file, 0, 0, ex.Message, origine);
                            }

                            processed++;

                            if (processed >= FilesPerBatch)
                            {
                                transaction.Commit();

                                savepointCommand.Dispose();
                                logCommand.Dispose();
                                markCommand.Dispose();
                                insertCommand.Dispose();
                                transaction.Dispose();

                                savepointCommand = null;
                                logCommand = null;
                                markCommand = null;
                                insertCommand = null;
                                transaction = null;
                                processed = 0;
                            }
                        }

                        if (transaction != null)
                        {
                            transaction.Commit();
                        }
                    }
                    finally
                    {
                        if (savepointCommand != null) savepointCommand.Dispose();
                        if (logCommand != null) logCommand.Dispose();
                        if (markCommand != null) markCommand.Dispose();
                        if (insertCommand != null) insertCommand.Dispose();
                        if (transaction != null) transaction.Dispose();
                    }
                }

                stopwatch.Stop();
                summary.DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);

                if (summary.CheckedFiles > 0)
                {
                    result.Add(summary);
                }
            }

            return result;
        }
    }
}
