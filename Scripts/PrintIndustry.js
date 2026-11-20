// PrintIndustry - gestione della dashboard e delle chiamate Web API.
// La logica applicativa e i calcoli restano nei Controllers C#.

var glob_macchine = [];
var glob_pagina_corrente = 1;
var glob_numero_pagine = 1;
var glob_grafici = [];
var glob_timer_ricerca = null;
var glob_origine_dato = "SIMULATO";
var glob_richiesta_origine = 0;
var glob_richiesta_stati = 0;
var glob_richiesta_lavorazioni = 0;
var glob_richiesta_statistiche = 0;
var glob_operazione_in_corso = false;


var glob_campi_economici = [
    ["HourCost", "Costo macchina €/h"],
    ["OperatorHourCost", "Operatore €/h"],
    ["PowerKw", "Potenza media kW"],
    ["EnergyEuroKwh", "Energia €/kWh"],
    ["InkCyanEuroLiter", "Cyan €/litro"],
    ["InkMagentaEuroLiter", "Magenta €/litro"],
    ["InkYellowEuroLiter", "Yellow €/litro"],
    ["InkBlackEuroLiter", "Black €/litro"],
    ["InkMlPerSqmEstimate", "Inchiostro stimato ml/m²"],
    ["MediaEuroSqm", "Materiale €/m² (stima)"],
    ["ToolEuroJob", "Utensili €/lavorazione (stima)"],
    ["AvailableHoursDay", "Ore disponibili al giorno"]
];

var glob_colori_grafici = [
    "#3b82f6", "#f59e0b", "#12b981", "#8b5cf6",
    "#ed719e", "#0891b2", "#84cc16", "#64748b"
];

function GetApiUrl(percorso) {
    var base = $("html").attr("data-app-base") || "/";

    if (base.charAt(base.length - 1) != "/") {
        base = base + "/";
    }

    return base + "api/" + percorso;
}

function GetApiError(xhr) {
    var messaggio = "Errore durante la comunicazione con il server.";

    if (xhr != null) {
        messaggio = "Errore HTTP " + xhr.status + ".";

        if (xhr.responseJSON != null && xhr.responseJSON.Message != null) {
            messaggio = xhr.responseJSON.Message;
        }
        else if (xhr.responseJSON != null && xhr.responseJSON.message != null) {
            messaggio = xhr.responseJSON.message;
        }
    }

    return messaggio;
}

function MostraAvviso(messaggio, titolo) {
    var testo = String(messaggio || "");

    if (testo == "") {
        return;
    }

    // Tutti gli avvisi dell'applicazione utilizzano Bootbox, mai alert() nativo.
    if (typeof bootbox == "undefined") {
        $("#message").text("Impossibile aprire il dialogo: libreria Bootbox non caricata. " + testo);
        return;
    }

    bootbox.alert({
        title: titolo || "PrintIndustry",
        message: EscapeHtml(testo).replace(/\n/g, "<br />"),
        centerVertical: true,
        buttons: {
            ok: {
                label: "OK",
                className: "btn-light"
            }
        }
    });
}

function MostraMessaggio(messaggio) {
    $("#message").text("");

    if (messaggio != null && messaggio != "") {
        MostraAvviso(messaggio, "PrintIndustry");
    }
}

function MostraStato(messaggio) {
    $("#message").text(messaggio || "");
}

function MostraLoadingPanel(testo) {
    if (glob_operazione_in_corso) {
        return false;
    }

    glob_operazione_in_corso = true;
    $("#loadingPanelText").text(testo || "Elaborazione in corso...");
    $("#loadingPanel").prop("hidden", false);
    $("header, main").prop("inert", true);
    $("body").addClass("operation-running");
    $("#loadingPanel").trigger("focus");

    return true;
}

function ChiudiLoadingPanel() {
    glob_operazione_in_corso = false;
    $("#loadingPanel").prop("hidden", true);
    $("header, main").prop("inert", false);
    $("body").removeClass("operation-running");
    $("#sourceAction").prop("disabled", false);
}

function EscapeHtml(valore) {
    return String(valore == null ? "" : valore)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function FormattaNumero(valore, decimali) {
    return Number(valore || 0).toLocaleString("it-IT", {
        minimumFractionDigits: decimali || 0,
        maximumFractionDigits: decimali || 0
    });
}

function FormattaEuro(valore) {
    return FormattaNumero(valore, 2) + " €";
}

function FormattaData(valore) {
    if (valore == null || valore == "") {
        return "—";
    }

    var parti = String(valore).substring(0, 19).replace("T", " ").split(" ");
    var data = parti[0].split("-");

    if (data.length != 3) {
        return "—";
    }

    return data[2] + "/" + data[1] + "/" + data[0] + " " + (parti[1] || "00:00:00");
}

function FormattaMinuti(secondi) {
    if (secondi == null) {
        return "—";
    }

    return FormattaNumero(Number(secondi) / 60, 2) + " min";
}

function FormattaValore(valore, decimali) {
    if (valore == null) {
        return "—";
    }

    return FormattaNumero(valore, decimali);
}

function ConvertiNumero(valore) {
    var testo = String(valore).trim().replace(/\s/g, "").replace(",", ".");

    if (!/^\d+(\.\d+)?$/.test(testo)) {
        return null;
    }

    var numero = Number(testo);
    return isFinite(numero) ? numero : null;
}

function MostraDecimale(valore) {
    if (valore == null || valore === "") {
        return "";
    }

    return String(valore).replace(".", ",");
}

function GetMacchinaSelezionata() {
    return $("#machine").val();
}

function GetMacchina() {
    var id = Number(GetMacchinaSelezionata());
    var macchina = null;

    for (var i = 0; i < glob_macchine.length; i++) {
        if (glob_macchine[i].Id == id) {
            macchina = glob_macchine[i];
            break;
        }
    }

    return macchina;
}

function GetFiltri() {
    return {
        machine: GetMacchinaSelezionata(),
        origine: glob_origine_dato,
        job: $("#jobFilter").val().trim(),
        status: $("#statusFilter").val(),
        from: $("#fromDate").val(),
        to: $("#toDate").val()
    };
}

function InizializzaPagina() {
    $(".nav").on("click", function () {
        var destinazione = $(this).attr("data-tab");
        MostraSezione(destinazione);
    });

    $("#goData").on("click", function () {
        MostraSezione("production");
    });

    $("#machine").on("change", CambioMacchina);
    $("#statusFilter, #fromDate, #toDate").on("change", ApplicaFiltri);
    $("#jobFilter").on("input", GestisciRicerca);
    $("#clearFrom").on("click", function () { CancellaData("fromDate"); });
    $("#clearTo").on("click", function () { CancellaData("toDate"); });
    $("#resetFilters").on("click", ResettaFiltri);
    $("#pageSize").on("change", CambiaNumeroRighe);

    $("#firstPage").on("click", PrimaPagina);
    $("#prevPage").on("click", PaginaPrecedente);
    $("#nextPage").on("click", PaginaSuccessiva);
    $("#lastPage").on("click", UltimaPagina);

    $("#workTabJobs").on("click", MostraLavorazioni);
    $("#workTabStats").on("click", MostraStatistiche);
    $("#sourceTabReal").on("click", SelezionaDatiReali);
    $("#sourceTabSimulated").on("click", SelezionaDatiSimulati);
    $("#sourceAction").on("click", EseguiOperazioneOrigine);
    $(document).on("click", "[data-save]", function () {
        SalvaParametriMacchina(Number($(this).attr("data-save")));
    });

    $(document).on("focusin focusout", "[data-field]", function () {
        $(this).val(MostraDecimale($(this).val()));
    });

    CaricaMacchine();
}

function MostraSezione(sezione) {
    $(".tab").prop("hidden", true);
    $("#" + sezione).prop("hidden", false);

    $(".nav").removeClass("active");
    $(".nav[data-tab='" + sezione + "']").addClass("active");

    if (sezione == "production") {
        AggiornaTitoloMacchina();
        $("#jobsPanel").prop("hidden", false);
        $("#statsPanel").prop("hidden", true);
        $("#workTabJobs").addClass("selected");
        $("#workTabStats").removeClass("selected");
        CaricaOriginePredefinita();
    }
}

function CaricaMacchine() {
    $.ajax({
        url: GetApiUrl("machines"),
        type: "GET",
        dataType: "json"
    }).done(CaricaMacchineSuccess).fail(CaricaMacchineError);
}

function CaricaMacchineSuccess(risultato) {
    glob_macchine = risultato || [];
    DisegnaConfigurazioni();
    MostraSezione("machines");
    MostraMessaggio("");
}

function CaricaMacchineError(xhr) {
    MostraMessaggio("Errore inizializzazione: " + GetApiError(xhr));
}

function GetCampiMacchina(macchina) {
    var campi = [];

    for (var i = 0; i < glob_campi_economici.length; i++) {
        var campo = glob_campi_economici[i];
        var nome = campo[0];
        var include = true;

        if (macchina.Code == "ZUND_G3") {
            include = ["HourCost", "OperatorHourCost", "PowerKw", "EnergyEuroKwh",
                "MediaEuroSqm", "ToolEuroJob", "AvailableHoursDay"].indexOf(nome) >= 0;
        }
        else {
            include = nome != "ToolEuroJob";
        }

        if (include) {
            campi.push(campo);
        }
    }

    return campi;
}

function DisegnaConfigurazioni() {
    var macchinaSelezionata = GetMacchinaSelezionata();
    var htmlSelect = "";
    var htmlConfigurazioni = "";

    for (var i = 0; i < glob_macchine.length; i++) {
        var macchina = glob_macchine[i];
        htmlSelect += '<option value="' + macchina.Id + '">' + EscapeHtml(macchina.Name) + '</option>';

        htmlConfigurazioni += '<div class="machineCard" data-machine="' + macchina.Id + '">';
        htmlConfigurazioni += '<h3>' + EscapeHtml(macchina.Name) + '</h3>';
        htmlConfigurazioni += '<p>Formato ' + EscapeHtml(macchina.Format) + '</p>';
        htmlConfigurazioni += '<div class="machineGrid">';

        var campi = GetCampiMacchina(macchina);

        for (var c = 0; c < campi.length; c++) {
            var campo = campi[c];
            htmlConfigurazioni += '<label>' + EscapeHtml(campo[1]);
            htmlConfigurazioni += '<input type="text" inputmode="decimal" data-field="' + campo[0] + '" ';
            htmlConfigurazioni += 'autocomplete="off" value="' + EscapeHtml(MostraDecimale(macchina[campo[0]])) + '" />';
            htmlConfigurazioni += '</label>';
        }

        htmlConfigurazioni += '</div>';
        htmlConfigurazioni += '<div class="page-actions">';
        htmlConfigurazioni += '<button type="button" data-save="' + macchina.Id + '">Salva parametri</button>';
        htmlConfigurazioni += '</div></div>';
    }

    $("#machine").html(htmlSelect);
    $("#machineForms").html(htmlConfigurazioni);

    if (macchinaSelezionata != null && macchinaSelezionata != "") {
        $("#machine").val(macchinaSelezionata);
    }

    if (!GetMacchinaSelezionata() && glob_macchine.length > 0) {
        $("#machine").val(String(glob_macchine[0].Id));
    }

    AggiornaTitoloMacchina();
}

function SalvaParametriMacchina(id) {
    var originale = null;

    for (var i = 0; i < glob_macchine.length; i++) {
        if (glob_macchine[i].Id == id) {
            originale = glob_macchine[i];
            break;
        }
    }

    if (originale == null) {
        return;
    }

    var macchina = $.extend({}, originale);
    var scheda = $(".machineCard[data-machine='" + id + "']");
    var valido = true;

    scheda.find("[data-field]").each(function () {
        var valore = ConvertiNumero($(this).val());

        if (valore == null) {
            MostraMessaggio("Valore numerico non valido: " + $(this).parent().text().trim());
            $(this).trigger("focus");
            valido = false;
            return false;
        }

        macchina[$(this).attr("data-field")] = valore;
    });

    if (!valido) {
        return;
    }

    $.ajax({
        url: GetApiUrl("machines/save"),
        type: "POST",
        data: JSON.stringify(macchina),
        contentType: "application/json; charset=utf-8",
        dataType: "json"
    }).done(function (risultato) {
        SalvaParametriSuccess(risultato, originale.Name);
    }).fail(SalvaParametriError);
}

function SalvaParametriSuccess(risultato, nome) {
    glob_macchine = risultato || [];
    DisegnaConfigurazioni();
    MostraMessaggio("Parametri salvati per " + nome + ".");
}

function SalvaParametriError(xhr) {
    MostraMessaggio("Errore salvataggio: " + GetApiError(xhr));
}

function AggiornaTitoloMacchina() {
    var macchina = GetMacchina();
    var nome = macchina == null ? "" : macchina.Name;

    $("#selectedMachineTitle").text(nome == "" ? "" : "— " + nome);
    $("#machineWorkTitle").text(nome == "" ? "Lavorazioni" : nome);
}

function CambioMacchina() {
    if (glob_operazione_in_corso) {
        return;
    }

    // La risposta precedente non deve mai popolare la macchina nuova.
    glob_richiesta_origine++;
    glob_richiesta_stati++;
    glob_richiesta_lavorazioni++;
    glob_richiesta_statistiche++;
    glob_pagina_corrente = 1;
    glob_numero_pagine = 1;
    $("#statusFilter").val("");

    SvuotaDatiMacchina();
    AggiornaTitoloMacchina();
    CaricaOriginePredefinita();
}

function SvuotaDatiMacchina() {
    $("#productionHead").empty();
    $("#productionRows").html('<tr><td>Caricamento dati della macchina selezionata...</td></tr>');
    $("#currentPage").text("1");
    $("#pageInfo").text("Caricamento dati...");
    $("#firstPage, #prevPage, #nextPage, #lastPage").prop("disabled", true);

    DistruggiGrafici();
    $("#machineDashboards").html("<p>Caricamento statistiche della macchina selezionata...</p>");
}

function CaricaOriginePredefinita() {
    AggiornaConteggiOrigine(true);
}

function AggiornaConteggiOrigine(selezioneAutomatica) {
    var idMacchina = GetMacchinaSelezionata();

    if (!idMacchina) {
        return;
    }

    glob_richiesta_origine++;
    var richiesta = glob_richiesta_origine;

    $.ajax({
        url: GetApiUrl("production/source-counts"),
        type: "GET",
        dataType: "json",
        data: { machine: idMacchina }
    }).done(function (risultato) {
        if (richiesta != glob_richiesta_origine || idMacchina != GetMacchinaSelezionata()) {
            return;
        }

        AggiornaConteggiOrigineSuccess(risultato, selezioneAutomatica);
    }).fail(function (xhr) {
        if (richiesta == glob_richiesta_origine && idMacchina == GetMacchinaSelezionata()) {
            AggiornaConteggiOrigineError(xhr);
        }
    });
}

function AggiornaConteggiOrigineSuccess(risultato, selezioneAutomatica) {
    var reali = Number(risultato.Reali || 0);
    var simulati = Number(risultato.Simulati || 0);

    $("#sourceTabReal").text("Dati reali (" + reali + ")");
    $("#sourceTabSimulated").text("Dati simulati (" + simulati + ")");

    if (selezioneAutomatica) {
        if (reali > 0) {
            glob_origine_dato = "REALE";
        }
        else {
            glob_origine_dato = "SIMULATO";
        }

        glob_pagina_corrente = 1;
        $("#statusFilter").val("");
    }

    AggiornaSchedaOrigine();

    if (selezioneAutomatica) {
        CaricaStati();
    }
}

function AggiornaConteggiOrigineError(xhr) {
    MostraMessaggio("Impossibile verificare l'origine dei dati: " + GetApiError(xhr));
}

function AggiornaSchedaOrigine() {
    var datiReali = glob_origine_dato == "REALE";

    $("#sourceTabReal").toggleClass("selected", datiReali);
    $("#sourceTabSimulated").toggleClass("selected", !datiReali);
    $("#sourceTabReal").attr("aria-pressed", datiReali ? "true" : "false");
    $("#sourceTabSimulated").attr("aria-pressed", datiReali ? "false" : "true");
    $("#sourceAction").text(datiReali ? "Importa dati reali" : "Genera dati simulati");
}

function SelezionaDatiReali() {
    SelezionaOrigine("REALE");
}

function SelezionaDatiSimulati() {
    SelezionaOrigine("SIMULATO");
}

function SelezionaOrigine(origine) {
    if (origine != "REALE" && origine != "SIMULATO") {
        return;
    }

    if (glob_origine_dato == origine) {
        // Anche un clic sulla scheda già visibile prevale su un controllo automatico in corso.
        glob_richiesta_origine++;
        AggiornaSchedaOrigine();
        CaricaStati();
        return;
    }

    // Evita che una risposta precedente ripristini una scheda non scelta dall'utente.
    glob_richiesta_origine++;
    glob_richiesta_stati++;
    glob_richiesta_lavorazioni++;
    glob_richiesta_statistiche++;
    glob_origine_dato = origine;
    glob_pagina_corrente = 1;
    $("#statusFilter").val("");

    SvuotaDatiMacchina();
    AggiornaSchedaOrigine();
    CaricaStati();
}

function EseguiOperazioneOrigine() {
    if (glob_origine_dato == "REALE") {
        ImportaDati();
    }
    else {
        GeneraDatiSimulati();
    }
}

function CaricaStati() {
    if (!GetMacchinaSelezionata()) {
        return;
    }

    glob_richiesta_stati++;
    var richiesta = glob_richiesta_stati;
    var macchinaRichiesta = GetMacchinaSelezionata();
    var origineRichiesta = glob_origine_dato;

    $.ajax({
        url: GetApiUrl("production/statuses"),
        type: "GET",
        dataType: "json",
        data: { machine: macchinaRichiesta, origine: origineRichiesta }
    }).done(function (risultato) {
        if (richiesta == glob_richiesta_stati &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaStatiSuccess(risultato);
        }
    }).fail(function (xhr) {
        if (richiesta == glob_richiesta_stati &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaStatiError(xhr);
        }
    });
}

function CaricaStatiSuccess(risultato) {
    var selezionato = $("#statusFilter").val();
    var html = '<option value="">Tutti gli stati</option>';
    var stati = risultato || [];

    for (var i = 0; i < stati.length; i++) {
        html += '<option value="' + EscapeHtml(stati[i]) + '">' + EscapeHtml(stati[i]) + '</option>';
    }

    $("#statusFilter").html(html);

    if (stati.indexOf(selezionato) >= 0) {
        $("#statusFilter").val(selezionato);
    }

    AggiornaDatiVisibili();
}

function CaricaStatiError(xhr) {
    MostraMessaggio("Errore stati: " + GetApiError(xhr));
}

function GestisciRicerca() {
    clearTimeout(glob_timer_ricerca);
    glob_timer_ricerca = setTimeout(ApplicaFiltri, 350);
}

function ApplicaFiltri() {
    glob_pagina_corrente = 1;
    AggiornaDatiVisibili();
}

function CancellaData(id) {
    $("#" + id).val("");
    ApplicaFiltri();
}

function ResettaFiltri() {
    // Macchina e numero di elementi per pagina non vengono modificati.
    $("#jobFilter").val("");
    $("#statusFilter").val("");
    $("#fromDate").val("");
    $("#toDate").val("");

    glob_pagina_corrente = 1;
    AggiornaDatiVisibili();
}

function CambiaNumeroRighe() {
    glob_pagina_corrente = 1;
    CaricaLavorazioni();
}

function PrimaPagina() {
    if (glob_pagina_corrente > 1) {
        glob_pagina_corrente = 1;
        CaricaLavorazioni();
    }
}

function PaginaPrecedente() {
    if (glob_pagina_corrente > 1) {
        glob_pagina_corrente--;
        CaricaLavorazioni();
    }
}

function PaginaSuccessiva() {
    if (glob_pagina_corrente < glob_numero_pagine) {
        glob_pagina_corrente++;
        CaricaLavorazioni();
    }
}

function UltimaPagina() {
    if (glob_pagina_corrente < glob_numero_pagine) {
        glob_pagina_corrente = glob_numero_pagine;
        CaricaLavorazioni();
    }
}

function AggiornaDatiVisibili() {
    if ($("#statsPanel").prop("hidden")) {
        CaricaLavorazioni();
    }
    else {
        CaricaStatistiche();
    }
}

function MostraLavorazioni() {
    $("#jobsPanel").prop("hidden", false);
    $("#statsPanel").prop("hidden", true);
    $("#workTabJobs").addClass("selected");
    $("#workTabStats").removeClass("selected");

    CaricaLavorazioni();
}

function MostraStatistiche() {
    $("#jobsPanel").prop("hidden", true);
    $("#statsPanel").prop("hidden", false);
    $("#workTabJobs").removeClass("selected");
    $("#workTabStats").addClass("selected");

    // I KPI e i grafici vengono richiesti soltanto quando questa scheda è attiva.
    CaricaStatistiche();
}

function CaricaLavorazioni() {
    if (!GetMacchinaSelezionata()) {
        return;
    }

    var filtri = GetFiltri();
    filtri.page = glob_pagina_corrente;
    filtri.pageSize = Number($("#pageSize").val());

    MostraStato("Caricamento dati...");
    $("#productionRows").html('<tr><td>Caricamento lavorazioni...</td></tr>');

    glob_richiesta_lavorazioni++;
    var richiesta = glob_richiesta_lavorazioni;
    var macchinaRichiesta = String(filtri.machine);
    var origineRichiesta = filtri.origine;

    $.ajax({
        url: GetApiUrl("production"),
        type: "GET",
        dataType: "json",
        data: filtri
    }).done(function (risultato) {
        if (richiesta == glob_richiesta_lavorazioni &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaLavorazioniSuccess(risultato, macchinaRichiesta, origineRichiesta);
        }
    }).fail(function (xhr) {
        if (richiesta == glob_richiesta_lavorazioni &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaLavorazioniError(xhr);
        }
    });
}

function CaricaLavorazioniSuccess(risultato, macchinaRichiesta, origineRichiesta) {
    var righe = risultato.Items || [];

    // Controllo difensivo: i dati di un'altra macchina non devono mai comparire.
    for (var i = 0; i < righe.length; i++) {
        if (String(righe[i].MachineId) != macchinaRichiesta ||
            righe[i].OrigineDato != origineRichiesta) {
            $("#productionRows").empty();
            MostraMessaggio("Errore: il server ha restituito lavorazioni di un'altra macchina o origine.");
            return;
        }
    }

    glob_pagina_corrente = risultato.Page;
    glob_numero_pagine = risultato.Pages;

    DisegnaLavorazioni(righe);
    AggiornaPaginazione(risultato);
    MostraMessaggio("");
}

function CaricaLavorazioniError(xhr) {
    MostraMessaggio("Errore caricamento dati: " + GetApiError(xhr));
}

function DisegnaLavorazioni(righe) {
    var macchina = GetMacchina();
    var taglio = macchina != null && macchina.Code == "ZUND_G3";
    var colonne = [];

    if (taglio) {
        colonne = ["Inizio", "Fine", "Lavorazione", "Stato", "Materiale", "Copie",
            "Interruzioni (min)", "Tempo operazioni (min)", "Costo totale stimato"];
    }
    else {
        colonne = ["Inizio", "Fine / Ready", "Lavorazione", "Stato", "Materiale",
            "Superficie m²", "Totale ml", "Cyan ml", "Magenta ml", "Yellow ml", "Black ml", "Tempo (min)", "Costo totale stimato"];
    }

    var intestazione = "<tr>";

    for (var c = 0; c < colonne.length; c++) {
        intestazione += "<th>" + EscapeHtml(colonne[c]) + "</th>";
    }

    intestazione += "</tr>";
    $("#productionHead").html(intestazione);

    var html = "";

    for (var i = 0; i < righe.length; i++) {
        var riga = righe[i];
        var valori = [];

        if (taglio) {
            valori = [
                FormattaData(riga.StartedAt),
                FormattaData(riga.FinishedAt),
                riga.JobName,
                riga.Status,
                riga.Media || "—",
                FormattaValore(riga.Copies, 0),
                FormattaMinuti(riga.IdleSeconds),
                FormattaMinuti(riga.AllocatedSeconds),
                FormattaEuro(riga.CostTotal)
            ];
        }
        else {
            valori = [
                FormattaData(riga.StartedAt),
                FormattaData(riga.FinishedAt),
                riga.JobName,
                riga.Status,
                riga.Media || "—",
                FormattaValore(riga.AreaSqm, 3),
                FormattaValore(riga.InkMl, 2),
                FormattaValore(riga.InkCyanMl, 2),
                FormattaValore(riga.InkMagentaMl, 2),
                FormattaValore(riga.InkYellowMl, 2),
                FormattaValore(riga.InkBlackMl, 2),
                FormattaMinuti(riga.AllocatedSeconds),
                FormattaEuro(riga.CostTotal)
            ];
        }

        html += "<tr>";

        for (var v = 0; v < valori.length; v++) {
            html += '<td title="' + EscapeHtml(valori[v]) + '">' + EscapeHtml(valori[v]) + '</td>';
        }

        html += "</tr>";
    }

    if (html == "") {
        html = '<tr><td colspan="' + colonne.length + '">Nessuna lavorazione trovata</td></tr>';
    }

    $("#productionRows").html(html);
}

function AggiornaPaginazione(risultato) {
    var primo = risultato.Total == 0 ? 0 : (risultato.Page - 1) * risultato.PageSize + 1;
    var ultimo = Math.min(risultato.Total, risultato.Page * risultato.PageSize);

    $("#currentPage").text(risultato.Page);
    $("#pageInfo").text("Pagina " + risultato.Page + " di " + risultato.Pages +
        ", elementi da " + primo + " a " + ultimo + " di " + risultato.Total + ".");

    $("#firstPage, #prevPage").prop("disabled", risultato.Page <= 1);
    $("#nextPage, #lastPage").prop("disabled", risultato.Page >= risultato.Pages);
}

function ImportaDati() {
    var macchina = GetMacchina();

    if (macchina == null) {
        return;
    }

    if (!MostraLoadingPanel("Importazione in corso...")) {
        return;
    }

    $("#sourceAction").prop("disabled", true);
    MostraMessaggio("");

    $.ajax({
        url: GetApiUrl("production/import?machine=" + encodeURIComponent(macchina.Id)),
        type: "POST",
        dataType: "json",
        timeout: 240000
    }).always(FineImportazione).done(ImportaDatiSuccess).fail(ImportaDatiError);
}

function ImportaDatiSuccess(risultato) {
    glob_pagina_corrente = 1;
    MostraPopupImportazione(GetRiepilogoImportazione(risultato), "Importazione completata");
    AggiornaConteggiOrigine(false);
    CaricaStati();
}

function ImportaDatiError(xhr, stato) {
    if (stato == "timeout") {
        MostraMessaggio("Il browser non ha ricevuto risposta entro 4 minuti. " +
            "Il server potrebbe stare ancora importando. Verifica i dati prima di riprovare; " +
            "le lavorazioni già salvate non verranno duplicate.");
    }
    else {
        MostraMessaggio("Errore importazione: " + GetApiError(xhr));
    }
}

function FineImportazione() {
    ChiudiLoadingPanel();
}

function GetRiepilogoImportazione(risultati) {
    var messaggi = [];
    var righe = risultati || [];

    for (var i = 0; i < righe.length; i++) {
        var riepilogo = righe[i];
        var nomeMacchina = riepilogo.Machine || "Macchina sconosciuta";
        var nuovi = Number(riepilogo.NewFiles || 0);
        var invariati = Number(riepilogo.UnchangedFiles || 0);
        var senzaNovita = Number(riepilogo.FilesWithoutNewData || 0);
        var errori = Number(riepilogo.Errors || 0);
        var lavori = Number(riepilogo.Inserted || 0);

        var messaggio = nomeMacchina + "\n" +
            nuovi + " file con nuovi dati, " + invariati + " già importati";

        if (senzaNovita > 0) {
            messaggio += ", " + senzaNovita + " senza nuove lavorazioni (copie o file vuoti)";
        }

        if (errori > 0) {
            messaggio += ", " + errori + " file con errore";
        }

        messaggio += ".\n" + lavori + " nuove lavorazioni salvate.";

        if (riepilogo.DurationSeconds != null) {
            messaggio += "\nTempo di importazione: " +
                Number(riepilogo.DurationSeconds).toLocaleString("it-IT") + " secondi.";
        }

        if (riepilogo.Message) {
            messaggio += "\nEsempi degli errori:\n" + riepilogo.Message;
        }

        messaggi.push(messaggio);
    }

    return messaggi.length > 0 ? messaggi.join("\n\n") : "Nessun file trovato.";
}

function GeneraDatiSimulati() {
    var macchina = GetMacchina();

    if (macchina == null || glob_operazione_in_corso) {
        return;
    }

    if (typeof bootbox == "undefined") {
        MostraStato("Libreria Bootbox non caricata: impossibile confermare la generazione.");
        return;
    }

    bootbox.confirm({
        title: "Generazione dati simulati",
        message: "Generare 30 giorni di dati simulati per <strong>" +
            EscapeHtml(macchina.Name) + "</strong>?",
        centerVertical: true,
        buttons: {
            cancel: {
                label: "Annulla",
                className: "btn-light"
            },
            confirm: {
                label: "Genera dati",
                className: "btn-light"
            }
        },
        callback: function (confermato) {
            if (confermato) {
                AvviaGenerazioneDatiSimulati(macchina.Id);
            }
        }
    });
}

function AvviaGenerazioneDatiSimulati(idMacchina) {
    // Se la macchina è cambiata mentre il dialogo era aperto, non eseguiamo l'operazione.
    if (String(idMacchina) != String(GetMacchinaSelezionata())) {
        MostraAvviso("La macchina selezionata è cambiata: ripeti l'operazione.");
        return;
    }

    if (!MostraLoadingPanel("Generazione in corso...")) {
        return;
    }

    $("#sourceAction").prop("disabled", true);
    MostraMessaggio("");

    $.ajax({
        url: GetApiUrl("production/simulate?machine=" + encodeURIComponent(idMacchina) + "&days=30"),
        type: "POST",
        dataType: "json",
        timeout: 240000
    }).always(FineGenerazione).done(GeneraDatiSuccess).fail(GeneraDatiError);
}

function GeneraDatiSuccess(risultato) {
    glob_pagina_corrente = 1;

    MostraPopupImportazione("Dati simulati\n" + risultato.Generated + " file generati, " +
        risultato.Imported + " nuove lavorazioni, " + risultato.Skipped +
        " già presenti, " + risultato.Errors + " errori.", "Generazione completata");

    AggiornaConteggiOrigine(false);
    CaricaStati();
}

function GeneraDatiError(xhr, stato) {
    if (stato == "timeout") {
        MostraMessaggio("Il browser non ha ricevuto risposta entro 4 minuti. " +
            "Controlla i dati e i file generati prima di riprovare; " +
            "le lavorazioni già salvate non verranno duplicate.");
    }
    else {
        MostraMessaggio("Errore simulazione: " + GetApiError(xhr));
    }
}

function FineGenerazione() {
    ChiudiLoadingPanel();
}

function MostraPopupImportazione(testo, titolo) {
    MostraAvviso(testo, titolo || "Operazione completata");
}

function CaricaStatistiche() {
    if ($("#statsPanel").prop("hidden")) {
        return;
    }

    var macchina = GetMacchina();

    if (macchina == null) {
        return;
    }

    if (typeof Chart == "undefined") {
        MostraMessaggio("Libreria Chart.js non disponibile.");
        return;
    }

    var richieste = [
        $.ajax({ url: GetApiUrl("production/kpi"), type: "GET", data: GetFiltri(), dataType: "json" }),
        $.ajax({ url: GetApiUrl("production/adaptive-trend"), type: "GET", data: GetFiltri(), dataType: "json" })
    ];

    if (macchina.Code == "COLORADO") {
        richieste.push($.ajax({
            url: GetApiUrl("production/consumptions"),
            type: "GET",
            data: GetFiltri(),
            dataType: "json"
        }));
    }

    glob_richiesta_statistiche++;
    var richiesta = glob_richiesta_statistiche;
    var macchinaRichiesta = GetMacchinaSelezionata();
    var origineRichiesta = glob_origine_dato;

    MostraStato("Caricamento statistiche...");
    $("#machineDashboards").html("<p>Caricamento statistiche...</p>");

    $.when.apply($, richieste).done(function (kpi, andamento, consumi) {
        if (richiesta == glob_richiesta_statistiche &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaStatisticheSuccess(kpi, andamento, consumi);
        }
    }).fail(function (xhr) {
        if (richiesta == glob_richiesta_statistiche &&
            macchinaRichiesta == GetMacchinaSelezionata() &&
            origineRichiesta == glob_origine_dato) {
            CaricaStatisticheError(xhr);
        }
    });
}

function CaricaStatisticheSuccess(rispostaKpi, rispostaAndamento, rispostaConsumi) {
    if ($("#statsPanel").prop("hidden")) {
        return;
    }

    var macchina = GetMacchina();
    var kpi = rispostaKpi[0];
    var andamento = rispostaAndamento[0];
    var consumi = rispostaConsumi == null ? null : rispostaConsumi[0];

    DistruggiGrafici();
    DisegnaStatistiche(macchina, kpi, andamento, consumi);
    MostraMessaggio("");
}

function CaricaStatisticheError(xhr) {
    MostraMessaggio("Errore statistiche: " + GetApiError(xhr));
}

function DistruggiGrafici() {
    for (var i = 0; i < glob_grafici.length; i++) {
        glob_grafici[i].destroy();
    }

    glob_grafici = [];
}

function GetTitoloAndamento(andamento) {
    if (!$("#fromDate").val() && !$("#toDate").val()) {
        return "Lavorazioni e costi negli ultimi 12 mesi";
    }

    if (andamento.Group == "year") {
        return "Lavorazioni e costi per anno";
    }
    else if (andamento.Group == "month") {
        return "Lavorazioni e costi per mese";
    }
    else if (andamento.Group == "fortnight") {
        return "Lavorazioni e costi ogni 15 giorni";
    }
    else {
        return "Lavorazioni e costi per giorno";
    }
}

function GetCardStatistiche(macchina, kpi) {
    var taglio = macchina.Code == "ZUND_G3";
    var costoMq = kpi.TotalAreaSqm > 0 ? kpi.TotalCost / kpi.TotalAreaSqm : null;
    var card = [
        ["Lavorazioni riuscite", FormattaNumero(kpi.GoodJobs) + " / " + FormattaNumero(kpi.Jobs),
            FormattaNumero(kpi.FailedJobs) + " non riuscite"],
        ["Superficie lavorata", FormattaNumero(kpi.TotalAreaSqm, 2) + " m²", ""],
        ["Tempo macchina", FormattaNumero(kpi.ActiveHours, 2) + " h", "lavori riusciti"],
        ["Produttività", kpi.SqmPerHour == null ? "—" : FormattaNumero(kpi.SqmPerHour, 2) + " m²/h", ""]
    ];

    // Efficienza: per la Zünd le interruzioni registrate, per la Colorado la saturazione sui turni.
    if (taglio) {
        card.push(["Interruzioni", FormattaNumero(kpi.IdleHours, 2) + " h", "registrate nei file"]);
    }
    else {
        card.push(["Saturazione", kpi.UtilizationPercent == null ? "—" : FormattaNumero(kpi.UtilizationPercent, 1) + " %",
            "ore lavorate / ore disponibili"]);
        card.push(["Inattività stimata", FormattaNumero(kpi.IdleHours, 2) + " h", "rispetto ai turni feriali"]);
    }

    card.push(["Tasso di scarto", kpi.ScrapPercent == null ? "n.d." : FormattaNumero(kpi.ScrapPercent, 2) + " %",
        kpi.ScrapPercent == null ? "scarto non presente nei dati" : FormattaNumero(kpi.Rejected) + " pezzi scartati"]);
    card.push(["Costo stimato", FormattaEuro(kpi.TotalCost), "somma dei costi storicizzati"]);
    card.push(["Costo per m²", costoMq == null ? "—" : FormattaEuro(costoMq), ""]);
    card.push(["Costo stimato dei fermi", FormattaEuro(kpi.IdleCostEstimate), "escluso dal costo totale"]);
    card.push(["Energia stimata", FormattaNumero(kpi.EstimatedKwh, 2) + " kWh", "potenza × ore"]);
    card.push(["Energia per m²", kpi.KwhPerSqm == null ? "—" : FormattaNumero(kpi.KwhPerSqm, 3) + " kWh/m²", ""]);

    var html = "";

    for (var i = 0; i < card.length; i++) {
        html += '<div class="card"><small>' + EscapeHtml(card[i][0]) + '</small>';
        html += '<strong>' + EscapeHtml(card[i][1]) + '</strong>';

        if (card[i][2]) {
            html += '<span class="card-note">' + EscapeHtml(card[i][2]) + '</span>';
        }

        html += '</div>';
    }

    return html;
}

function DisegnaStatistiche(macchina, kpi, andamento, consumi) {
    var colorado = macchina.Code == "COLORADO";
    var html = '<section class="machineAnalysis">';

    html += '<div class="cards kpi-cards">' + GetCardStatistiche(macchina, kpi) + '</div>';
    html += '<div class="charts">';
    html += '<article><h2>Composizione costi</h2><div class="chartFrame"><canvas id="costChart"></canvas></div></article>';
    html += '<article><h2>' + EscapeHtml(GetTitoloAndamento(andamento)) + '</h2>';
    html += '<div class="chartFrame"><canvas id="trendChart"></canvas></div></article>';

    if (colorado) {
        html += '<article><h2>Inchiostri CMYK · consumo e costo stimato</h2>';
        html += '<div class="chartFrame"><canvas id="inkChart"></canvas></div></article>';
        html += '<article><h2>Materiali più utilizzati · m² e costo stimato</h2>';
        html += '<div class="chartFrame"><canvas id="mediaChart"></canvas></div>';
        html += '<div id="materialsLegend" class="materials-legend"></div>';
        html += '<div id="materialsDetail" class="materials-detail" hidden></div>';
        html += '<p>Costi dei materiali stimati con la tariffa €/m² configurata.</p></article>';
    }

    html += '</div>';
    html += '<p>' + EscapeHtml(kpi.Note || "") + '</p>';
    html += '</section>';

    $("#machineDashboards").html(html);

    DisegnaGraficoCosti(macchina, kpi);
    DisegnaGraficoAndamento(andamento);

    if (colorado && consumi != null) {
        DisegnaGraficoInchiostri(consumi.Inks || []);
        DisegnaGraficoMateriali(consumi.Materials || []);
    }
}

function DisegnaGraficoCosti(macchina, kpi) {
    var taglio = macchina.Code == "ZUND_G3";
    var etichette = taglio
        ? ["Macchina", "Operatore", "Energia", "Materiali", "Utensili"]
        : ["Macchina", "Operatore", "Energia", "Inchiostri", "Materiali"];
    var valori = taglio
        ? [kpi.MachineCost, kpi.OperatorCost, kpi.EnergyCost, kpi.MediaCost, kpi.ToolCost]
        : [kpi.MachineCost, kpi.OperatorCost, kpi.EnergyCost, kpi.InkCost, kpi.MediaCost];

    var grafico = new Chart(document.getElementById("costChart"), {
        type: "bar",
        data: {
            labels: etichette,
            datasets: [{
                label: "Costo stimato (€)",
                data: valori,
                backgroundColor: glob_colori_grafici
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } }
        }
    });

    glob_grafici.push(grafico);
}

function GetEtichettaAndamento(periodo, tipo) {
    var date = String(periodo).split("|");
    var inizio = date[0].split("-");

    if (tipo == "year") {
        return inizio[0];
    }
    else if (tipo == "month") {
        return inizio[1] + "/" + inizio[0];
    }
    else if (tipo == "fortnight") {
        var fine = date.length > 1 ? date[1].split("-") : inizio;
        return inizio[2] + "/" + inizio[1] + " – " + fine[2] + "/" + fine[1];
    }
    else {
        return inizio[2] + "/" + inizio[1];
    }
}

function DisegnaGraficoAndamento(andamento) {
    var righe = andamento.Items || [];
    var etichette = [];
    var lavorazioni = [];
    var costi = [];

    for (var i = 0; i < righe.length; i++) {
        etichette.push(GetEtichettaAndamento(righe[i].Day, andamento.Group));
        lavorazioni.push(righe[i].Jobs);
        costi.push(Number(righe[i].Cost || 0));
    }

    // Due serie su assi separati: numero di lavorazioni (sinistra) e costo stimato in euro (destra).
    var grafico = new Chart(document.getElementById("trendChart"), {
        type: "bar",
        data: {
            labels: etichette,
            datasets: [{
                type: "line",
                label: "Lavorazioni",
                data: lavorazioni,
                borderColor: "#1862ac",
                backgroundColor: "#1862ac",
                tension: 0,
                fill: false,
                pointRadius: 3,
                yAxisID: "y"
            }, {
                type: "bar",
                label: "Costo stimato (€)",
                data: costi,
                backgroundColor: "rgba(232, 146, 46, 0.55)",
                yAxisID: "y1"
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: { beginAtZero: true, position: "left", ticks: { precision: 0 },
                    title: { display: true, text: "Lavorazioni" } },
                y1: { beginAtZero: true, position: "right", grid: { drawOnChartArea: false },
                    title: { display: true, text: "€" } }
            },
            plugins: {
                legend: { display: true, position: "bottom" },
                tooltip: {
                    callbacks: {
                        label: function (contesto) {
                            if (contesto.dataset.yAxisID == "y1") {
                                return "Costo stimato: " + FormattaEuro(contesto.parsed.y);
                            }

                            return contesto.parsed.y + " lavorazioni";
                        },
                        afterBody: function (elementi) {
                            var riga = elementi.length > 0 ? righe[elementi[0].dataIndex] : null;

                            if (riga == null) {
                                return "";
                            }

                            return [
                                "Superficie: " + FormattaNumero(riga.AreaSqm, 2) + " m²",
                                "Ore macchina: " + FormattaNumero(riga.Hours, 2) + " h",
                                "Energia stimata: " + FormattaNumero(riga.EnergyKwh, 2) + " kWh"
                            ];
                        }
                    }
                }
            }
        }
    });

    glob_grafici.push(grafico);
}

function DisegnaGraficoInchiostri(inchiostri) {
    var etichette = [];
    var valori = [];

    for (var i = 0; i < inchiostri.length; i++) {
        etichette.push(inchiostri[i].Name);
        valori.push(inchiostri[i].Ml);
    }

    var grafico = new Chart(document.getElementById("inkChart"), {
        type: "doughnut",
        data: {
            labels: etichette,
            datasets: [{ data: valori, backgroundColor: ["#21adcf", "#d658b8", "#e9c52b", "#323b4b"] }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                tooltip: {
                    callbacks: {
                        label: function (contesto) {
                            var colore = inchiostri[contesto.dataIndex];
                            return FormattaNumero(colore.Ml, 2) + " ml · " + FormattaEuro(colore.Cost) + " (stima)";
                        }
                    }
                }
            }
        }
    });

    glob_grafici.push(grafico);
}

function DisegnaGraficoMateriali(materiali) {
    var principali = materiali.slice(0, 7);
    var altri = materiali.slice(7);
    var dati = principali.slice();
    var areaAltri = 0;
    var costoAltri = 0;

    for (var i = 0; i < altri.length; i++) {
        areaAltri += Number(altri[i].AreaSqm || 0);
        costoAltri += Number(altri[i].Cost || 0);
    }

    if (altri.length > 0) {
        dati.push({ Name: "Altri (" + altri.length + ")", AreaSqm: areaAltri, Cost: costoAltri });
    }

    var etichette = [];
    var valori = [];
    var legenda = "";

    for (var n = 0; n < dati.length; n++) {
        var materiale = dati[n];
        var altro = n == dati.length - 1 && altri.length > 0;

        etichette.push(materiale.Name);
        valori.push(materiale.AreaSqm);

        legenda += '<div class="material-legend-item' + (altro ? ' material-legend-other' : '') + '"';
        legenda += altro ? ' role="button" tabindex="0" title="Visualizza altri materiali"' : '';
        legenda += '>';
        legenda += '<span class="material-legend-swatch" style="background:' + glob_colori_grafici[n] + '"></span>';
        legenda += '<span class="material-legend-name">' + EscapeHtml(materiale.Name) + '</span>';
        legenda += '<span class="material-legend-amount">' + FormattaNumero(materiale.AreaSqm, 2) + ' m²</span>';
        legenda += '</div>';
    }

    $("#materialsLegend").html(legenda);

    var grafico = new Chart(document.getElementById("mediaChart"), {
        type: "doughnut",
        data: {
            labels: etichette,
            datasets: [{ data: valori, backgroundColor: glob_colori_grafici }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: "44%",
            onClick: function (evento, elementi) {
                if (elementi.length > 0 && altri.length > 0 && elementi[0].index == dati.length - 1) {
                    ApriDettaglioMateriali();
                }
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (contesto) {
                            var materiale = dati[contesto.dataIndex];
                            return FormattaNumero(materiale.AreaSqm, 2) + " m² · " +
                                FormattaEuro(materiale.Cost) + " (stima)";
                        },
                        afterLabel: function (contesto) {
                            if (altri.length > 0 && contesto.dataIndex == dati.length - 1) {
                                return "Clicca per visualizzare i " + altri.length + " materiali raggruppati";
                            }
                            return "";
                        }
                    }
                }
            }
        }
    });

    glob_grafici.push(grafico);

    var dettaglio = '<div class="materials-detail-head">';
    dettaglio += '<strong>Altri materiali (' + altri.length + ')</strong>';
    dettaglio += '<button type="button" id="closeMaterials">Chiudi</button>';
    dettaglio += '</div><div class="materials-scroll"><table>';
    dettaglio += '<thead><tr><th>Materiale</th><th>Consumo</th><th>Costo stimato</th></tr></thead><tbody>';

    for (var m = 0; m < altri.length; m++) {
        dettaglio += '<tr><td>' + EscapeHtml(altri[m].Name) + '</td>';
        dettaglio += '<td>' + FormattaNumero(altri[m].AreaSqm, 2) + ' m²</td>';
        dettaglio += '<td>' + FormattaEuro(altri[m].Cost) + '</td></tr>';
    }

    dettaglio += '</tbody></table></div>';
    $("#materialsDetail").html(dettaglio).prop("hidden", true);

    $(".material-legend-other").on("click", ApriDettaglioMateriali);
    $(".material-legend-other").on("keydown", function (evento) {
        if (evento.key == "Enter" || evento.key == " ") {
            evento.preventDefault();
            ApriDettaglioMateriali();
        }
    });

    $("#closeMaterials").on("click", ChiudiDettaglioMateriali);
}

function ApriDettaglioMateriali() {
    $("#materialsDetail").prop("hidden", false);
}

function ChiudiDettaglioMateriali() {
    $("#materialsDetail").prop("hidden", true);
}

$(document).ready(InizializzaPagina);
