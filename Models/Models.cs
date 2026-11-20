using System;

namespace PrintIndustry.Models
{
    public class Machine
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public decimal HourCost { get; set; }
        public decimal OperatorHourCost { get; set; }
        public decimal PowerKw { get; set; }
        public decimal EnergyEuroKwh { get; set; }
        public decimal InkEuroLiter { get; set; }
        public decimal InkCyanEuroLiter { get; set; }
        public decimal InkMagentaEuroLiter { get; set; }
        public decimal InkYellowEuroLiter { get; set; }
        public decimal InkBlackEuroLiter { get; set; }
        public decimal InkMlPerSqmEstimate { get; set; }
        public decimal MediaEuroSqm { get; set; }
        public decimal ToolEuroJob { get; set; }
        public decimal AvailableHoursDay { get; set; }
        public bool Configured { get; set; }
        public bool Enabled { get; set; }
    }
    public class ProductionData
    {
        public long Id { get; set; }
        public int MachineId { get; set; }
        public string SourceKey { get; set; }
        public string OrigineDato { get; set; }
        public string JobName { get; set; }
        public string GroupKey { get; set; }
        public string Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int? ActiveSeconds { get; set; }
        public int? IdleSeconds { get; set; }
        public int? Copies { get; set; }
        public int? Rejected { get; set; }
        public decimal? WidthMm { get; set; }
        public decimal? HeightMm { get; set; }
        public decimal? AreaSqm { get; set; }
        public decimal? InkMl { get; set; }
        public decimal? InkCyanMl { get; set; }
        public decimal? InkMagentaMl { get; set; }
        public decimal? InkYellowMl { get; set; }
        public decimal? InkBlackMl { get; set; }
        public string Media { get; set; }
        public string PrintMode { get; set; }
        public bool Successful { get; set; }
        public decimal AllocatedSeconds { get; set; }

        // Parametri e costi fissati alla prima importazione.
        public DateTime? CostCapturedAt { get; set; }
        public string CostSnapshotSource { get; set; }
        public decimal SnapHourCost { get; set; }
        public decimal SnapOperatorHourCost { get; set; }
        public decimal SnapPowerKw { get; set; }
        public decimal SnapEnergyEuroKwh { get; set; }
        public decimal SnapInkEuroLiter { get; set; }
        public decimal SnapInkCyanEuroLiter { get; set; }
        public decimal SnapInkMagentaEuroLiter { get; set; }
        public decimal SnapInkYellowEuroLiter { get; set; }
        public decimal SnapInkBlackEuroLiter { get; set; }
        public decimal SnapInkMlPerSqmEstimate { get; set; }
        public decimal SnapMediaEuroSqm { get; set; }
        public decimal SnapToolEuroJob { get; set; }
        public decimal CostMachine { get; set; }
        public decimal CostOperator { get; set; }
        public decimal CostEnergy { get; set; }
        public decimal CostInk { get; set; }
        public decimal CostInkCyan { get; set; }
        public decimal CostInkMagenta { get; set; }
        public decimal CostInkYellow { get; set; }
        public decimal CostInkBlack { get; set; }
        public decimal CostMedia { get; set; }
        public decimal CostTool { get; set; }
        public decimal CostIdleEstimate { get; set; }
        public decimal EnergyKwh { get; set; }
        public decimal CostTotal { get; set; }
    }
    public class OriginiMacchina
    {
        public int Reali { get; set; }
        public int Simulati { get; set; }
    }

    public class ImportResult
    {
        public string Machine { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public int NewFiles { get; set; }
        public int UnchangedFiles { get; set; }
        public int FilesWithoutNewData { get; set; }
        public int CheckedFiles { get; set; }
        public double DurationSeconds { get; set; }
        public string Message { get; set; }
    }
    public class Kpi
    {
        public int Jobs { get; set; }
        public int GoodJobs { get; set; }
        public int FailedJobs { get; set; }
        public decimal TotalAreaSqm { get; set; }
        public decimal ActiveHours { get; set; }
        public decimal IdleHours { get; set; }
        public decimal MachineCost { get; set; }
        public decimal OperatorCost { get; set; }
        public decimal EnergyCost { get; set; }
        public decimal InkCost { get; set; }
        public decimal MediaCost { get; set; }
        public decimal ToolCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal IdleCostEstimate { get; set; }
        public decimal EstimatedKwh { get; set; }
        public decimal? KwhPerSqm { get; set; }
        public decimal? UtilizationPercent { get; set; }
        public decimal? SqmPerHour { get; set; }
        public int? Produced { get; set; }
        public int? Rejected { get; set; }
        public decimal? ScrapPercent { get; set; }
        public string Note { get; set; }
    }

    public class DailyProduction
    {
        public string Day { get; set; }
        public int Jobs { get; set; }
        public int GoodJobs { get; set; }
        public decimal Hours { get; set; }
        public decimal AreaSqm { get; set; }
        public decimal EnergyKwh { get; set; }
        public decimal Cost { get; set; }
    }
}
