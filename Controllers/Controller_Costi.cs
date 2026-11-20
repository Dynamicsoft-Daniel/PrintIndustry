using System;
using PrintIndustry.Models;

namespace PrintIndustry.Controllers
{
    // Congela il listino attuale sulla singola lavorazione. Non usa mai prezzi futuri.
    public class Controller_Costi
    {
        public void Calcola(ProductionData lavoro, Machine macchina)
        {
            try
            {
                if (lavoro == null || macchina == null)
                {
                    throw new ArgumentException("Lavorazione o macchina mancante.");
                }

                lavoro.CostCapturedAt = DateTime.UtcNow;
                lavoro.CostSnapshotSource = "IMPORTAZIONE";

                lavoro.SnapHourCost = macchina.HourCost;
                lavoro.SnapOperatorHourCost = macchina.OperatorHourCost;
                lavoro.SnapPowerKw = macchina.PowerKw;
                lavoro.SnapEnergyEuroKwh = macchina.EnergyEuroKwh;
                lavoro.SnapInkEuroLiter = macchina.InkEuroLiter;
                lavoro.SnapInkCyanEuroLiter = macchina.InkCyanEuroLiter;
                lavoro.SnapInkMagentaEuroLiter = macchina.InkMagentaEuroLiter;
                lavoro.SnapInkYellowEuroLiter = macchina.InkYellowEuroLiter;
                lavoro.SnapInkBlackEuroLiter = macchina.InkBlackEuroLiter;
                lavoro.SnapInkMlPerSqmEstimate = macchina.InkMlPerSqmEstimate;
                lavoro.SnapMediaEuroSqm = macchina.MediaEuroSqm;
                lavoro.SnapToolEuroJob = macchina.ToolEuroJob;

                decimal ore = lavoro.AllocatedSeconds / 3600m;
                lavoro.CostMachine = ore * macchina.HourCost;
                lavoro.CostOperator = ore * macchina.OperatorHourCost;
                lavoro.EnergyKwh = ore * macchina.PowerKw;
                lavoro.CostEnergy = lavoro.EnergyKwh * macchina.EnergyEuroKwh;
                lavoro.CostIdleEstimate = (lavoro.IdleSeconds ?? 0) / 3600m * macchina.HourCost;

                if (lavoro.Successful)
                {
                    if (macchina.Code == "COLORADO")
                    {
                        bool dettagliColori = lavoro.InkCyanMl.HasValue || lavoro.InkMagentaMl.HasValue ||
                            lavoro.InkYellowMl.HasValue || lavoro.InkBlackMl.HasValue;

                        if (dettagliColori)
                        {
                            lavoro.CostInkCyan = (lavoro.InkCyanMl ?? 0m) / 1000m * macchina.InkCyanEuroLiter;
                            lavoro.CostInkMagenta = (lavoro.InkMagentaMl ?? 0m) / 1000m * macchina.InkMagentaEuroLiter;
                            lavoro.CostInkYellow = (lavoro.InkYellowMl ?? 0m) / 1000m * macchina.InkYellowEuroLiter;
                            lavoro.CostInkBlack = (lavoro.InkBlackMl ?? 0m) / 1000m * macchina.InkBlackEuroLiter;
                            lavoro.CostInk = lavoro.CostInkCyan + lavoro.CostInkMagenta +
                                lavoro.CostInkYellow + lavoro.CostInkBlack;
                        }
                        else
                        {
                            decimal? ml = lavoro.InkMl;
                            if (!ml.HasValue && lavoro.AreaSqm.HasValue && macchina.InkMlPerSqmEstimate > 0)
                            {
                                ml = lavoro.AreaSqm.Value * macchina.InkMlPerSqmEstimate;
                            }

                            if (ml.HasValue)
                            {
                                lavoro.CostInk = ml.Value / 1000m * macchina.InkEuroLiter;
                            }
                        }
                    }

                    if (lavoro.AreaSqm.HasValue)
                    {
                        lavoro.CostMedia = lavoro.AreaSqm.Value * macchina.MediaEuroSqm;
                    }

                    if (macchina.Code == "ZUND_G3")
                    {
                        lavoro.CostTool = macchina.ToolEuroJob;
                    }
                }

                lavoro.CostTotal = lavoro.CostMachine + lavoro.CostOperator + lavoro.CostEnergy +
                    lavoro.CostInk + lavoro.CostMedia + lavoro.CostTool;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
