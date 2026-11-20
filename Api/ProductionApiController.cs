using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Results;
using PrintIndustry.Controllers;
using PrintIndustry.Models;

namespace PrintIndustry.Api
{
    [RoutePrefix("api/production")]
    public class ProductionApiController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult List(
            int? machine = null,
            int page = 1,
            int pageSize = 10,
            string status = null,
            string from = null,
            string to = null,
            string job = null,
            string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                DateTime? dataInizio;
                DateTime? dataFine;
                string errore;

                if (!ReadRange(from, to, out dataInizio, out dataFine, out errore))
                {
                    return BadRequest(errore);
                }

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                page = Math.Max(1, page);
                pageSize = Math.Max(10, Math.Min(100, pageSize));

                int totale = controller.ContaLavorazioni(machine, dataInizio, dataFine, status, job, origine);
                int pagine = Math.Max(1, (int)Math.Ceiling((double)totale / pageSize));
                page = Math.Min(page, pagine);

                result = Ok(new
                {
                    Items = controller.Lavorazioni(machine, pageSize, dataInizio, dataFine,
                        status, (page - 1) * pageSize, job, origine),
                    Total = totale,
                    Page = page,
                    PageSize = pageSize,
                    Pages = pagine
                });
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("statuses")]
        public IHttpActionResult Statuses(int? machine = null, string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                string errore;

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.Stati(machine, origine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("kpi")]
        public IHttpActionResult Kpis(
            int? machine = null,
            string from = null,
            string to = null,
            string status = null,
            string job = null,
            string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                DateTime? dataInizio;
                DateTime? dataFine;
                string errore;

                if (!ReadRange(from, to, out dataInizio, out dataFine, out errore))
                {
                    return BadRequest(errore);
                }

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.Statistiche(machine, dataInizio, dataFine, status, job, origine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("trend")]
        public IHttpActionResult Trend(
            int? machine = null,
            string from = null,
            string to = null,
            string status = null,
            string job = null,
            string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                DateTime? dataInizio;
                DateTime? dataFine;
                string errore;

                if (!ReadRange(from, to, out dataInizio, out dataFine, out errore))
                {
                    return BadRequest(errore);
                }

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.Andamento(machine.Value, dataInizio, dataFine, status, job, origine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("adaptive-trend")]
        public IHttpActionResult AdaptiveTrend(
            int? machine = null,
            string from = null,
            string to = null,
            string status = null,
            string job = null,
            string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                DateTime? dataInizio;
                DateTime? dataFine;
                string errore;

                if (!ReadRange(from, to, out dataInizio, out dataFine, out errore))
                {
                    return BadRequest(errore);
                }

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.AndamentoAdattivo(machine.Value, dataInizio, dataFine, status, job, origine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("consumptions")]
        public IHttpActionResult Consumptions(
            int? machine = null,
            string from = null,
            string to = null,
            string status = null,
            string job = null,
            string origine = "REALE")
        {
            IHttpActionResult result = null;

            try
            {
                DateTime? dataInizio;
                DateTime? dataFine;
                string errore;

                if (!ReadRange(from, to, out dataInizio, out dataFine, out errore))
                {
                    return BadRequest(errore);
                }

                if (!ControllaRichiesta(machine, ref origine, out errore))
                {
                    return BadRequest(errore);
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.ConsumiColorado(machine.Value, dataInizio, dataFine, status, job, origine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpGet]
        [Route("source-counts")]
        public IHttpActionResult SourceCounts(int? machine = null)
        {
            IHttpActionResult result = null;

            try
            {
                if (!machine.HasValue)
                {
                    return BadRequest("Selezionare un macchinario.");
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.ContaOrigini(machine.Value));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpPost]
        [Route("import")]
        public IHttpActionResult Import(int? machine = null)
        {
            IHttpActionResult result = null;

            try
            {
                if (!machine.HasValue) {
                    return BadRequest("Selezionare un macchinario prima di importare.");
                }

                Controller_Produzione controller = new Controller_Produzione();

                if (machine.HasValue && !controller.Macchine().Any(x => x.Id == machine.Value))
                {
                    return BadRequest("Macchinario non valido.");
                }

                result = Ok(controller.ImportaDati(machine));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpPost]
        [Route("simulate")]
        public IHttpActionResult Simulate(int? machine = null, int days = 30, int? seed = null)
        {
            IHttpActionResult result = null;

            try
            {
                if (!machine.HasValue)
                {
                    return BadRequest("Selezionare un macchinario.");
                }

                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.GeneraDati(machine.Value, days, seed));
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        // Controlla macchina e origine comuni a tutte le interrogazioni sui dati di produzione.
        private static bool ControllaRichiesta(int? machine, ref string origine, out string errore)
        {
            bool result = true;
            errore = null;

            if (!machine.HasValue)
            {
                errore = "Selezionare un macchinario.";
                result = false;
            }
            else if (!new Controller_Produzione().Macchine().Any(x => x.Id == machine.Value))
            {
                errore = "Macchinario non valido.";
                result = false;
            }
            else
            {
                origine = string.IsNullOrWhiteSpace(origine) ? "REALE" : origine.Trim().ToUpperInvariant();

                if (origine != "REALE" && origine != "SIMULATO")
                {
                    errore = "Origine non valida: usare REALE o SIMULATO.";
                    result = false;
                }
            }

            return result;
        }

        private static bool ReadRange(
            string from,
            string to,
            out DateTime? dataInizio,
            out DateTime? dataFine,
            out string errore)
        {
            bool result = true;
            DateTime data;

            dataInizio = null;
            dataFine = null;
            errore = null;

            if (!string.IsNullOrWhiteSpace(from))
            {
                if (DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out data))
                {
                    dataInizio = data;
                }
                else
                {
                    errore = "Data iniziale non valida.";
                    result = false;
                }
            }

            if (result && !string.IsNullOrWhiteSpace(to))
            {
                if (DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out data))
                {
                    dataFine = data.AddDays(1);
                }
                else
                {
                    errore = "Data finale non valida.";
                    result = false;
                }
            }

            if (result && dataInizio.HasValue && dataFine.HasValue && dataInizio >= dataFine)
            {
                errore = "Intervallo date non valido.";
                result = false;
            }

            return result;
        }
    }

    [RoutePrefix("api/machines")]
    public class MachinesApiController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult List()
        {
            IHttpActionResult result = null;

            try
            {
                Controller_Produzione controller = new Controller_Produzione();
                result = Ok(controller.Macchine());
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }

        [HttpPost]
        [Route("save")]
        public IHttpActionResult Save([FromBody] Machine machine)
        {
            IHttpActionResult result = null;

            try
            {
                if (machine == null)
                {
                    return BadRequest("Configurazione mancante.");
                }

                Controller_Produzione controller = new Controller_Produzione();
                controller.SalvaMacchina(machine);
                result = Ok(controller.Macchine());
            }
            catch (ArgumentException ex)
            {
                result = BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                result = RisposteApi.ErroreInterno(this, ex);
            }

            return result;
        }
    }

    // Risposta comune agli errori non previsti: il dettaglio tecnico va solo nel log,
    // all'utente arriva un messaggio generico senza informazioni interne del server.
    internal static class RisposteApi
    {
        public static IHttpActionResult ErroreInterno(ApiController controller, Exception ex)
        {
            Trace.TraceError("PrintIndustry {0} {1}: {2}",
                controller.Request != null ? controller.Request.Method.ToString() : "",
                controller.Request != null ? controller.Request.RequestUri.PathAndQuery : "",
                ex);

            return new NegotiatedContentResult<object>(HttpStatusCode.InternalServerError,
                new { Message = "Errore interno del server: l'operazione non è stata completata. " +
                    "Il dettaglio è registrato nel log dell'applicazione." },
                controller);
        }
    }
}
