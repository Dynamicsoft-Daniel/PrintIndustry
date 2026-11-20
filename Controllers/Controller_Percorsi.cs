using System;
using System.IO;

namespace PrintIndustry.Controllers
{
    /// <summary>
    /// Accesso ai tracciati anche quando il progetto si trova in percorsi Windows lunghi.
    /// La cartella e il nome originale del file non vengono modificati.
    /// </summary>
    public static class Controller_Percorsi
    {
        public static string Normalizza(string percorso)
        {
            string result = percorso;

            try
            {
                if (string.IsNullOrWhiteSpace(percorso))
                {
                    throw new ArgumentException("Il percorso del file non è valorizzato.");
                }

                if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                    !percorso.StartsWith(@"\\?\", StringComparison.Ordinal))
                {
                    string percorsoCompleto = Path.IsPathRooted(percorso)
                        ? percorso : Path.GetFullPath(percorso);

                    // Il prefisso Win32 esteso è necessario solo per i percorsi lunghi.
                    if (percorsoCompleto.Length >= 240)
                    {
                        if (percorsoCompleto.StartsWith(@"\\", StringComparison.Ordinal))
                        {
                            result = @"\\?\UNC\" + percorsoCompleto.Substring(2);
                        }
                        else
                        {
                            result = @"\\?\" + percorsoCompleto;
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

        public static FileInfo InformazioniFile(string percorso)
        {
            FileInfo result = null;

            try
            {
                result = new FileInfo(Normalizza(percorso));
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        public static FileStream ApriFile(string percorso)
        {
            FileStream result = null;

            try
            {
                result = new FileStream(Normalizza(percorso), FileMode.Open,
                    FileAccess.Read, FileShare.Read);
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }
    }
}
