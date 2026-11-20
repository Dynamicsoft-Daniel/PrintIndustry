using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PrintIndustry.Controllers
{
    public static class ColoradoSimulator
    {
        // Il generatore produce CSV nello stesso formato 4302/4303 utilizzato dal parser reale.
        public static int Generate(string folder, DateTime firstDay, int workingDays, int seed, string batchCode)
        {
            int result = 0;

            try
            {
                Directory.CreateDirectory(folder);

                Random random = new Random(seed);
                string[] header = new string[]
                {
                    "4302", "documentid", "jobid", "jobtype", "jobname", "printmode",
                    "startdate", "starttime", "activetime", "idletime", "readydate",
                    "readytime", "accountid", "noffinishedsets", "result", "receptiondate",
                    "receptiontime", "operatornote", "copiesrequested", "mediatypeid",
                    "mediatype", "mediawidth", "medialengthused", "printedarea",
                    "inkcolorcyan", "inkcolormagenta", "inkcoloryellow", "inkcolorblack"
                };

                string[] materials = new string[] { "Vinile", "Banner", "Carta", "Canvas" };
                int generatedDays = 0;

                for (DateTime date = firstDay.Date; generatedDays < workingDays; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    generatedDays++;

                    StringBuilder fileText = new StringBuilder();
                    fileText.AppendLine(string.Join(";", header));

                    DateTime clock = date.AddHours(8);
                    int jobs = 3 + random.Next(5);

                    for (int index = 0; index < jobs; index++)
                    {
                        int minutes = 5 + random.Next(75);

                        // Circa un lavoro su quattro ha un fermo (attese, cambio supporto) da 1 a 15 minuti.
                        int idleMinutes = random.Next(4) == 0 ? 1 + random.Next(15) : 0;

                        // Circa il 6% dei lavori non va a buon fine, con gli stessi stati dei file reali.
                        double esito = random.NextDouble();
                        string stato = esito < 0.04 ? "Abrt" : esito < 0.06 ? "Stop" : "Done";

                        DateTime finishedAt = clock.AddMinutes(minutes + idleMinutes);
                        string jobKey = "SIM-" + batchCode + "-" + date.ToString("yyyyMMdd") + "-" + index;
                        string[] row = new string[header.Length];

                        for (int column = 0; column < row.Length; column++)
                        {
                            row[column] = string.Empty;
                        }

                        row[0] = "4303";

                        Set(row, header, "documentid", jobKey);
                        Set(row, header, "jobid", jobKey);
                        Set(row, header, "jobname", "SIM-Colorado-" + batchCode + "-" + date.ToString("yyyyMMdd") + "-" + index);
                        Set(row, header, "startdate", clock.ToString("yyyy-MM-dd"));
                        Set(row, header, "starttime", clock.ToString("HH:mm:ss"));
                        Set(row, header, "readydate", finishedAt.ToString("yyyy-MM-dd"));
                        Set(row, header, "readytime", finishedAt.ToString("HH:mm:ss"));
                        Set(row, header, "activetime", TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm\:ss"));
                        Set(row, header, "idletime", TimeSpan.FromMinutes(idleMinutes).ToString(@"hh\:mm\:ss"));
                        Set(row, header, "result", stato);
                        Set(row, header, "printedarea", (1000000 * (1 + random.Next(12))).ToString(CultureInfo.InvariantCulture));
                        Set(row, header, "mediatype", materials[random.Next(materials.Length)]);
                        Set(row, header, "inkcolorcyan", (1500 + random.Next(2500)).ToString(CultureInfo.InvariantCulture));
                        Set(row, header, "inkcolormagenta", (1500 + random.Next(2500)).ToString(CultureInfo.InvariantCulture));
                        Set(row, header, "inkcoloryellow", (1500 + random.Next(2500)).ToString(CultureInfo.InvariantCulture));
                        Set(row, header, "inkcolorblack", (1500 + random.Next(2500)).ToString(CultureInfo.InvariantCulture));

                        fileText.AppendLine(string.Join(";", row));
                        clock = finishedAt.AddMinutes(5);
                    }

                    string fileName = "SIM-COLORADO-" + batchCode + "-" + date.ToString("yyyyMMdd") + ".CSV";
                    File.WriteAllText(Path.Combine(folder, fileName), fileText.ToString(), new UTF8Encoding(false));
                    result++;
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        private static void Set(string[] row, string[] headers, string key, string value)
        {
            for (int index = 0; index < headers.Length; index++)
            {
                if (string.Equals(headers[index], key, StringComparison.OrdinalIgnoreCase))
                {
                    row[index] = value;
                    return;
                }
            }
        }
    }
}
