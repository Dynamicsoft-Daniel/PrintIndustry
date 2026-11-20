using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace PrintIndustry.Controllers
{
    public static class ZundSimulator
    {
        // I tracciati creati da questa classe sono dati simulati, non misure della Zünd.
        public static int Generate(string folder, DateTime start, int workingDays, int seed, string batchCode)
        {
            int result = 0;

            try
            {
                Directory.CreateDirectory(folder);

                Random random = new Random(seed);
                string[] materials = new string[]
                {
                    "FOREX 3 - 5mm",
                    "ADESIVO PVC",
                    "COMMUNICATION 10 - 19mm",
                    "CARTONE 3 mm"
                };

                int generatedDays = 0;

                for (DateTime date = start.Date; generatedDays < workingDays; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    generatedDays++;

                    int jobs = 3 + random.Next(4);
                    DateTime clock = date.AddHours(8);

                    for (int index = 0; index < jobs; index++)
                    {
                        int cuttingSeconds = 120 + random.Next(900);
                        int setupSeconds = 15 + random.Next(110);
                        int interruptionSeconds = random.Next(5) == 0 ? 10 + random.Next(240) : 0;
                        int copies = 1 + random.Next(12);
                        int rejected = random.NextDouble() < 0.08 ? 1 : 0;

                        DateTime startedAt = clock;
                        DateTime finishedAt = startedAt.AddSeconds(cuttingSeconds + setupSeconds + interruptionSeconds);

                        string jobKey = "SIM-" + batchCode + "-" + date.ToString("yyyyMMdd") + "-" + index;
                        string fileName = jobKey + ".xml";

                        XElement productionTime = new XElement("ProductionTime",
                            new XElement("Parameter",
                                new XAttribute("MethodName", "Thru-cut"),
                                new XAttribute("MethodTime", cuttingSeconds.ToString(CultureInfo.InvariantCulture)),
                                new XAttribute("Tool", "UCT")),
                            new XElement("Parameter",
                                new XAttribute("MethodName", "Setup"),
                                new XAttribute("MethodTime", setupSeconds.ToString(CultureInfo.InvariantCulture))),
                            new XElement("Parameter",
                                new XAttribute("MethodName", "Interrupt"),
                                new XAttribute("MethodTime", interruptionSeconds.ToString(CultureInfo.InvariantCulture))));

                        XElement job = new XElement("JobStatus",
                            new XAttribute("Name", jobKey + ".zcc"),
                            new XAttribute("Status", "done"),
                            new XAttribute("CutterGUID", "G3_3XL1600_SIM"),
                            new XAttribute("Client", "G3 3XL-1600 - SIMULATED"),
                            new XAttribute("DoneCopies", copies),
                            new XAttribute("TotalCopies", copies + rejected),
                            new XAttribute("CopySizeX", (300 + random.Next(700)).ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("CopySizeY", (350 + random.Next(900)).ToString(CultureInfo.InvariantCulture)),
                            new XAttribute("Material", materials[random.Next(materials.Length)]),
                            new XAttribute("StartTime", startedAt.ToString("yyyy-MM-ddTHH:mm:ss")),
                            new XAttribute("EndTime", finishedAt.ToString("yyyy-MM-ddTHH:mm:ss")),
                            productionTime);

                        XDocument document = new XDocument(
                            new XElement("ZCC_cmd",
                                new XAttribute("MessageID", jobKey),
                                new XAttribute("CommandID", "jobstatus"),
                                new XAttribute("Version", "3040"),
                                job));

                        document.Save(Path.Combine(folder, fileName));
                        result++;

                        clock = finishedAt.AddMinutes(4 + random.Next(8));
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }
    }
}
