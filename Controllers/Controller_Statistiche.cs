using System;
using System.Collections.Generic;
using System.Linq;
using PrintIndustry.DAL;
using PrintIndustry.Models;

namespace PrintIndustry.Controllers
{
    // Indicatori di produzione calcolati sulle lavorazioni già salvate e sui loro costi storicizzati.
    public class Controller_Statistiche
    {
        public Kpi Calcola(int? machine, DateTime? from = null, DateTime? to = null,
            string status = null, string job = null, string origine = "REALE")
        {
            List<ProductionData> records = Db.Productions(machine, 100000, from, to, status, 0, job, origine);
            Dictionary<int, Machine> machines = Db.Machines().ToDictionary(x => x.Id);

            if (machine.HasValue && !machines.ContainsKey(machine.Value))
            {
                throw new ArgumentException("Macchinario non valido.");
            }

            Kpi result = new Kpi
            {
                Jobs = records.Count,
                GoodJobs = records.Count(x => x.Successful),
                FailedJobs = records.Count(x => !x.Successful),
                TotalAreaSqm = records.Where(x => x.Successful).Sum(x => x.AreaSqm ?? 0)
            };

            List<ProductionData> good = records.Where(x => x.Successful).ToList();
            result.ActiveHours = good.Sum(x => x.AllocatedSeconds) / 3600m;

            if (machine.HasValue && machines[machine.Value].Code == "ZUND_G3")
            {
                result.IdleHours = good.Sum(x => x.IdleSeconds ?? 0) / 3600m;
            }

            List<ProductionData> dated = records.Where(x => x.StartedAt.HasValue).ToList();

            if (dated.Count > 0 && machine.HasValue && machines[machine.Value].Code != "ZUND_G3")
            {
                DateTime first = (from ?? dated.Min(x => x.StartedAt.Value)).Date;
                DateTime last = (to.HasValue ? to.Value.AddTicks(-1) : dated.Max(x => x.StartedAt.Value)).Date;

                if (last >= first)
                {
                    Machine selected = machines[machine.Value];
                    decimal available = 0m;
                    Dictionary<DateTime, decimal> byDay = dated.GroupBy(x => x.StartedAt.Value.Date)
                        .ToDictionary(x => x.Key,
                            x => x.Sum(r => r.Successful ? r.AllocatedSeconds : 0m) / 3600m);

                    for (DateTime day = first; day <= last; day = day.AddDays(1))
                    {
                        decimal worked = byDay.ContainsKey(day) ? byDay[day] : 0m;
                        bool weekday = day.DayOfWeek != DayOfWeek.Saturday &&
                            day.DayOfWeek != DayOfWeek.Sunday;
                        decimal capacity = weekday ? selected.AvailableHoursDay : worked;

                        available += capacity;
                        result.IdleHours += Math.Max(0m, capacity - worked);
                    }

                    if (available > 0m)
                    {
                        result.UtilizationPercent = Math.Min(100m, 100m * result.ActiveHours / available);
                    }
                }
            }

            // Le statistiche utilizzano i costi congelati, non il listino corrente.
            foreach (ProductionData item in records)
            {
                result.MachineCost += item.CostMachine;
                result.OperatorCost += item.CostOperator;
                result.EnergyCost += item.CostEnergy;
                result.InkCost += item.CostInk;
                result.MediaCost += item.CostMedia;
                result.ToolCost += item.CostTool;
                result.IdleCostEstimate += item.CostIdleEstimate;
                result.EstimatedKwh += item.EnergyKwh;
                result.TotalCost += item.CostTotal;
            }

            result.SqmPerHour = result.ActiveHours > 0
                ? result.TotalAreaSqm / result.ActiveHours : (decimal?)null;
            result.KwhPerSqm = result.TotalAreaSqm > 0
                ? result.EstimatedKwh / result.TotalAreaSqm : (decimal?)null;

            List<ProductionData> relevant = records
                .Where(x => x.Rejected.HasValue && x.Copies.HasValue).ToList();

            if (relevant.Count > 0)
            {
                result.Produced = relevant.Sum(x => x.Copies ?? 0);
                result.Rejected = relevant.Sum(x => x.Rejected ?? 0);
                int total = result.Produced.Value + result.Rejected.Value;
                result.ScrapPercent = total > 0
                    ? 100m * result.Rejected.Value / total : (decimal?)null;
            }

            bool cutting = machine.HasValue && machines[machine.Value].Code == "ZUND_G3";
            bool simulati = string.Equals(origine, "SIMULATO", StringComparison.OrdinalIgnoreCase);
            result.Note = cutting
                ? "Zünd G3: tempi e interruzioni dai tracciati XML. Energia stimata con " +
                  "potenza configurata (3,6 kW iniziali, aspirazione esclusa); " +
                  (simulati ? "scarti generati dal simulatore." : "scarti non rilevati nei file reali.")
                : "Colorado: energia stimata sulle ore di stampa. Inattività stimata rispetto " +
                  "ai turni feriali, non un fermo macchina misurato.";

            return result;
        }
    }
}
