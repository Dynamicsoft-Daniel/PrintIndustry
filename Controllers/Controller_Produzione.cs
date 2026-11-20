using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PrintIndustry.DAL;
using PrintIndustry.Models;

namespace PrintIndustry.Controllers
{
    public class Controller_Produzione
    {
        public List<Machine> Macchine()
        {
            List<Machine> result = new List<Machine>();
            try
            {
                result = Db.Machines();
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public OriginiMacchina ContaOrigini(int machineId)
        {
            OriginiMacchina result = new OriginiMacchina();

            try
            {
                if (!Db.Machines().Any(x => x.Id == machineId))
                {
                    throw new ArgumentException("Macchinario non valido.");
                }

                result = Db.ContaOrigini(machineId);
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        public void SalvaMacchina(Machine machine)
        {
            try
            {
                Db.SaveMachine(machine);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public List<ProductionData> Lavorazioni(int? machine, int pageSize, DateTime? from, DateTime? to,
            string state, int offset, string job, string origine = "REALE")
        {
            List<ProductionData> result = new List<ProductionData>();
            try
            {
                result = Db.Productions(machine, pageSize, from, to, state, offset, job, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public int ContaLavorazioni(int? machine, DateTime? from, DateTime? to, string state, string job, string origine = "REALE")
        {
            int result = 0;
            try
            {
                result = Db.ProductionCount(machine, from, to, state, job, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public List<string> Stati(int? machine, string origine = "REALE")
        {
            List<string> result = new List<string>();
            try
            {
                result = Db.ProductionStatuses(machine, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public Kpi Statistiche(int? machine, DateTime? from, DateTime? to, string state, string job, string origine = "REALE")
        {
            Kpi result = new Kpi();
            try
            {
                Controller_Statistiche controller = new Controller_Statistiche();
                result = controller.Calcola(machine, from, to, state, job, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public List<DailyProduction> Andamento(int machine, DateTime? from, DateTime? to,
            string state, string job, string origine = "REALE")
        {
            List<DailyProduction> result = new List<DailyProduction>();
            try
            {
                result = Db.DailyTrend(machine, from, to, state, job, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public object AndamentoAdattivo(int machine, DateTime? a, DateTime? b, string status, string job, string origine = "REALE")
        {
            // Senza filtro data: ultimi 12 mesi, compreso il mese corrente.
            DateTime defaultEnd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
            DateTime defaultStart = defaultEnd.AddMonths(-12);
            DateTime start = a ?? (b.HasValue ? b.Value.AddMonths(-12) : defaultStart);
            DateTime end = b ?? (a.HasValue ? DateTime.Today.AddDays(1) : defaultEnd);

            if (start >= end)
            {
                throw new ArgumentException("Intervallo date non valido.");
            }

            double days = (end - start).TotalDays;
            string grouping;

            // Fino a 30 giorni: giorni; fino a 60: blocchi di 15 giorni; oltre: mesi; oltre 12 mesi: anni.
            if (end > start.AddMonths(12))
            {
                grouping = "year";
            }
            else if (days > 60)
            {
                grouping = "month";
            }
            else if (days > 30)
            {
                grouping = "fortnight";
            }
            else
            {
                grouping = "day";
            }

            List<DailyProduction> raw = Db.PeriodTrend(machine, start, end,
                grouping == "fortnight" ? "day" : grouping, status, job, origine);

            if (grouping == "fortnight")
            {
                // Blocchi consecutivi da 15 giorni a partire dal primo giorno del filtro.
                List<DailyProduction> fortnight = new List<DailyProduction>();

                for (DateTime day = start.Date; day < end; day = day.AddDays(15))
                {
                    DateTime first = day;
                    DateTime last = day.AddDays(15) < end ? day.AddDays(15) : end;
                    IEnumerable<DailyProduction> items = raw.Where(x =>
                    {
                        DateTime dt;
                        return DateTime.TryParseExact(x.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out dt) && dt >= first && dt < last;
                    });

                    fortnight.Add(new DailyProduction
                    {
                        Day = day.ToString("yyyy-MM-dd") + "|" + last.AddDays(-1).ToString("yyyy-MM-dd"),
                        Jobs = items.Sum(x => x.Jobs),
                        GoodJobs = items.Sum(x => x.GoodJobs),
                        Hours = items.Sum(x => x.Hours),
                        AreaSqm = items.Sum(x => x.AreaSqm),
                        EnergyKwh = items.Sum(x => x.EnergyKwh),
                        Cost = items.Sum(x => x.Cost)
                    });
                }

                return new
                {
                    Group = grouping,
                    Items = fortnight
                };
            }

            Dictionary<string, DailyProduction> data = raw.ToDictionary(x => x.Day);
            List<DailyProduction> points = new List<DailyProduction>();
            string format = grouping == "year" ? "yyyy" : grouping == "month" ? "yyyy-MM" : "yyyy-MM-dd";
            DateTime cursor;

            if (grouping == "year")
            {
                cursor = new DateTime(start.Year, 1, 1);
            }
            else if (grouping == "month")
            {
                cursor = new DateTime(start.Year, start.Month, 1);
            }
            else
            {
                cursor = start.Date;
            }

            while (cursor < end)
            {
                string key = cursor.ToString(format, CultureInfo.InvariantCulture);
                DailyProduction item;

                if (!data.TryGetValue(key, out item))
                {
                    item = new DailyProduction
                    {
                        Day = key,
                        Jobs = 0,
                        GoodJobs = 0,
                        Hours = 0
                    };
                }

                points.Add(item);

                // Difesa contro intervalli non ragionevoli, senza bloccare il server.
                if (points.Count >= 2000)
                {
                    break;
                }

                if (grouping == "year")
                {
                    cursor = cursor.AddYears(1);
                }
                else if (grouping == "month")
                {
                    cursor = cursor.AddMonths(1);
                }
                else
                {
                    cursor = cursor.AddDays(1);
                }
            }

            return new
            {
                Group = grouping,
                Items = points
            };
        }

        public object ConsumiColorado(int machine, DateTime? from, DateTime? to, string state, string job, string origine = "REALE")
        {
            object result = null;
            try
            {
                result = Db.ColoradoConsumptions(machine, from, to, state, job, origine);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public List<ImportResult> ImportaDati(int? machine)
        {
            List<ImportResult> result = new List<ImportResult>();
            try
            {
                result = Controller_Importazione.ImportFolder(machine, false);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public object GeneraDati(int machineId, int days, int? seed)
        {
            object result = null;

            try
            {
                if (days < 1 || days > 365)
                {
                    throw new ArgumentException("Periodo valido: 1–365 giorni lavorativi.");
                }

                Machine machine = Db.Machines().FirstOrDefault(x => x.Id == machineId);

                if (machine == null)
                {
                    throw new ArgumentException("Macchinario non valido.");
                }

                // In modalità normale ogni clic crea un lotto nuovo.
                // Il seed esplicito mantiene una simulazione riproducibile per i test PW30.
                Guid runId = Guid.NewGuid();
                int seedUsed = seed.HasValue ? seed.Value : (runId.GetHashCode() & int.MaxValue);
                string batchCode = seed.HasValue
                    ? "TEST-" + seed.Value.ToString(CultureInfo.InvariantCulture)
                    : runId.ToString("N").Substring(0, 12);

                string folder = Path.Combine(
                    Db.Location,
                    "Imports",
                    machine.Code == "ZUND_G3" ? "ZUND_G3" : "COLORADO_1650",
                    "Simulati");

                // Le lavorazioni simulate coprono gli ultimi giorni lavorativi,
                // terminando oggi (oppure venerdì, quando si esegue nel weekend).
                DateTime firstDay = DateTime.Today;
                int workingDays = 0;

                while (workingDays < days)
                {
                    if (firstDay.DayOfWeek != DayOfWeek.Saturday &&
                        firstDay.DayOfWeek != DayOfWeek.Sunday)
                    {
                        workingDays++;
                    }

                    if (workingDays < days)
                    {
                        firstDay = firstDay.AddDays(-1);
                    }
                }

                int generated = machine.Code == "ZUND_G3"
                    ? ZundSimulator.Generate(folder, firstDay, days, seedUsed, batchCode)
                    : ColoradoSimulator.Generate(folder, firstDay, days, seedUsed, batchCode);

                List<ImportResult> imported = Controller_Importazione.ImportFolder(machineId, true);

                result = new
                {
                    Generated = generated,
                    Imported = imported.Sum(x => x.Inserted),
                    Skipped = imported.Sum(x => x.Skipped),
                    Errors = imported.Sum(x => x.Errors),
                    Seed = seedUsed,
                    Riproducibile = seed.HasValue
                };
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }
    }
}
