using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PrintIndustry.Models;

namespace PrintIndustry.Controllers
{
    public static class ParseUtil
    {
        public static decimal? Dec(string v)
        {
            decimal n;
            return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out n)
                ? n : (decimal?)null;
        }

        public static int? Int(string v)
        {
            int n;
            return int.TryParse(v, out n) ? n : (int?)null;
        }

        public static int? Seconds(string v)
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                return null;
            }

            string[] a = v.Split(':');
            long x;

            try
            {
                if (a.Length == 3)
                {
                    x = long.Parse(a[0]) * 3600 + long.Parse(a[1]) * 60 + long.Parse(a[2]);
                }
                else if (a.Length == 2)
                {
                    x = long.Parse(a[0]) * 60 + long.Parse(a[1]);
                }
                else
                {
                    return null;
                }

                return x > int.MaxValue ? (int?)null : (int)x;
            }
            catch
            {
                return null;
            }
        }

        public static string Sha(string v)
        {
            using (SHA256 h = SHA256.Create())
            {
                byte[] hash = h.ComputeHash(Encoding.UTF8.GetBytes(v));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string Txt(XElement x, string name)
        {
            return (string)x.Element(name);
        }
    }

    public interface IProductionParser
    {
        IEnumerable<ProductionData> Parse(string file, Machine machine);
    }

    public class ZundG3Parser : IProductionParser
    {
        private static string Attr(XElement e, string key)
        {
            return e == null ? null : (string)e.Attribute(key);
        }

        private static DateTime? Date(string x)
        {
            DateTime d;
            return DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                ? d : (DateTime?)null;
        }

        private static int Seconds(decimal n)
        {
            return (int)Math.Round(n, MidpointRounding.AwayFromZero);
        }

        public IEnumerable<ProductionData> Parse(string file, Machine machine)
        {
            XElement root = null;

            using (FileStream stream = Controller_Percorsi.ApriFile(file))
            {
                root = XDocument.Load(stream).Root;
            }

            if (root == null || root.Name.LocalName != "ZCC_cmd" || Attr(root, "CommandID") != "jobstatus")
            {
                throw new InvalidDataException("Zünd: tracciato jobstatus non valido");
            }

            List<XElement> jobs = root.Elements().Where(x => x.Name.LocalName == "JobStatus").ToList();

            if (jobs.Count == 0)
            {
                throw new InvalidDataException("Zünd: JobStatus assente");
            }

            foreach (XElement job in jobs)
            {
                string status = Attr(job, "Status") ?? "";
                XElement paramsNode = job.Elements().FirstOrDefault(x => x.Name.LocalName == "ProductionTime");
                List<XElement> entries = paramsNode == null
                    ? new List<XElement>()
                    : paramsNode.Elements().Where(x => x.Name.LocalName == "Parameter").ToList();
                decimal work = 0m;
                decimal interrupted = 0m;

                foreach (XElement e in entries)
                {
                    decimal sec = ParseUtil.Dec(Attr(e, "MethodTime")) ?? 0m;

                    if (Attr(e, "MethodName") == "Interrupt")
                    {
                        interrupted += sec;
                    }
                    else
                    {
                        work += sec;
                    }
                }

                DateTime? started = Date(Attr(job, "StartTime"));
                DateTime? finished = Date(Attr(job, "EndTime"));
                double totalSeconds = started.HasValue && finished.HasValue
                    ? Math.Max(0, (finished.Value - started.Value).TotalSeconds) : 0;

                // I tempi metodo sono cumulativi per categoria e possono non coincidere con la durata di orologio.
                int active = work > 0m ? Seconds(work) : Seconds((decimal)totalSeconds);
                int? copies = ParseUtil.Int(Attr(job, "DoneCopies"));
                int? totalCopies = ParseUtil.Int(Attr(job, "TotalCopies"));
                decimal? w = ParseUtil.Dec(Attr(job, "CopySizeX"));
                decimal? h = ParseUtil.Dec(Attr(job, "CopySizeY"));

                // Copie * ingombro geometrico: NON equivale necessariamente al materiale consumato.
                decimal? area = w.HasValue && h.HasValue && copies.HasValue
                    ? w.Value * h.Value * copies.Value / 1000000m : (decimal?)null;
                string name = Attr(job, "Name") ?? Path.GetFileNameWithoutExtension(file);
                bool simulato = name.StartsWith("SIM-", StringComparison.Ordinal);
                int? rejected = simulato && copies.HasValue && totalCopies.HasValue
                    ? Math.Max(0, totalCopies.Value - copies.Value) : (int?)null;

                // Distingue i passaggi ripetuti della stessa commessa tramite timestamp e MessageID.
                string rawKey = (Attr(root, "MessageID") ?? "") + "|" + name + "|" +
                    (Attr(job, "StartTime") ?? "") + "|" + (Attr(job, "EndTime") ?? "");
                string key = (simulato ? "SIM-" : "REAL-") + ParseUtil.Sha(rawKey);

                yield return new ProductionData
                {
                    MachineId = machine.Id,
                    SourceKey = key,
                    JobName = name,
                    GroupKey = Attr(job, "Material"),
                    Status = status,
                    Successful = string.Equals(status, "done", StringComparison.OrdinalIgnoreCase),
                    StartedAt = started,
                    FinishedAt = finished,
                    ActiveSeconds = active,
                    IdleSeconds = Seconds(interrupted),
                    Copies = copies,
                    Rejected = rejected,
                    WidthMm = w,
                    HeightMm = h,
                    AreaSqm = area,
                    Media = Attr(job, "Material"),
                    PrintMode = "ZCC"
                };
            }
        }
    }

    public class ColoradoParser : IProductionParser
    {
        // Conversioni interne al tracciato Canon: superficie in unità 1e-6 m², inchiostro in µl.
        // Fattori ricavati dalla scala dei campioni e verificati con l'azienda sui lavori eseguiti.
        private const decimal AreaRawToSqm = 0.000001m;
        private const decimal InkRawToMl = 0.001m;

        // Lettura CSV separato da punto e virgola con campi tra virgolette (anche su più righe), come RFC 4180.
        private static List<string[]> Rows(string file)
        {
            string text = string.Empty;

            using (FileStream stream = Controller_Percorsi.ApriFile(file))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                text = reader.ReadToEnd().TrimStart('﻿');
            }

            List<string[]> rows = new List<string[]>();
            List<string> cells = new List<string>();
            StringBuilder b = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        b.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ';' && !quoted)
                {
                    cells.Add(b.ToString());
                    b.Clear();
                }
                else if ((c == '\n' || c == '\r') && !quoted)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    cells.Add(b.ToString());
                    b.Clear();

                    if (cells.Any(x => x.Length > 0))
                    {
                        rows.Add(cells.ToArray());
                    }

                    cells.Clear();
                }
                else
                {
                    b.Append(c);
                }
            }

            if (cells.Count > 0 || b.Length > 0)
            {
                cells.Add(b.ToString());
                rows.Add(cells.ToArray());
            }

            if (quoted)
            {
                throw new InvalidDataException("CSV: virgolette non chiuse");
            }

            return rows;
        }

        public IEnumerable<ProductionData> Parse(string file, Machine m)
        {
            List<string[]> rows = Rows(file);

            if (rows.Count == 0)
            {
                yield break;
            }

            string[] header = rows[0];

            if (header[0] != "4302")
            {
                throw new InvalidDataException("Colorado: intestazione 4302 mancante");
            }

            Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < header.Length; i++)
            {
                columns[header[i]] = i;
            }

            string[] fields = new[]
            {
                "documentid", "jobname", "result", "activetime", "printedarea", "inkcolorcyan"
            };

            if (fields.Any(x => !columns.ContainsKey(x)))
            {
                throw new InvalidDataException("Colorado: colonne richieste mancanti");
            }

            Dictionary<string, int> occurrences = new Dictionary<string, int>();

            foreach (string[] r in rows.Skip(1))
            {
                if (r.Length == 0 || r[0] != "4303")
                {
                    continue;
                }

                if (r.Length != header.Length)
                {
                    throw new InvalidDataException("Colorado: numero colonne non coerente");
                }

                Func<string, string> v = name => columns.ContainsKey(name) ? r[columns[name]] : null;
                string canonical = string.Join(";", r.Select(x => x.Replace(";", ";;")));
                int n;
                occurrences.TryGetValue(canonical, out n);
                occurrences[canonical] = n + 1;

                DateTime date;
                string dt = (v("startdate") ?? "") + " " + (v("starttime") ?? "");
                DateTime? started = DateTime.TryParseExact(dt, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date : (DateTime?)null;

                List<decimal?> inks = new[]
                {
                    "inkcolorcyan", "inkcolormagenta", "inkcoloryellow", "inkcolorblack"
                }
                .Select(x => ParseUtil.Dec(v(x))).ToList();
                decimal? ink = inks.All(x => x.HasValue) ? inks.Sum(x => x.Value) * InkRawToMl : (decimal?)null;

                DateTime ready;
                string finishText = (v("readydate") ?? "") + " " + (v("readytime") ?? "");
                DateTime? finished = DateTime.TryParseExact(finishText, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out ready) ? ready : (DateTime?)null;
                string status = v("result");

                yield return new ProductionData
                {
                    MachineId = m.Id,
                    SourceKey = ParseUtil.Sha(canonical + "|" + n),
                    JobName = v("jobname"),
                    GroupKey = null,
                    Status = status,
                    Successful = status == "Done",
                    StartedAt = started,
                    FinishedAt = finished,
                    InkCyanMl = inks[0] * InkRawToMl,
                    InkMagentaMl = inks[1] * InkRawToMl,
                    InkYellowMl = inks[2] * InkRawToMl,
                    InkBlackMl = inks[3] * InkRawToMl,
                    ActiveSeconds = ParseUtil.Seconds(v("activetime")),
                    IdleSeconds = ParseUtil.Seconds(v("idletime")),
                    Copies = ParseUtil.Int(v("noffinishedsets")),
                    AreaSqm = ParseUtil.Dec(v("printedarea")) * AreaRawToSqm,
                    InkMl = ink,
                    Media = v("mediatype"),
                    PrintMode = v("printmode")
                };
            }
        }
    }
}
