<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="PrintIndustry.Views.Dashboard" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="it" data-app-base="<%= ResolveUrl("~/") %>">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>PrintIndustry Monitor</title>

    <%-- Bootstrap è necessario per Bootbox; il CSS specifico di PrintIndustry mantiene lo stile esistente. --%>
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/vendor/bootstrap.min.css") %>" />
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/site.css?v=20261113a") %>" />

    <script src="<%= ResolveUrl("~/Scripts/vendor/jquery.min.js") %>" defer></script>
    <script src="<%= ResolveUrl("~/Scripts/vendor/bootstrap.bundle.min.js") %>" defer></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/bootbox.js/6.0.4/bootbox.min.js" defer></script>
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.7/dist/chart.umd.min.js" defer></script>
    <script src="<%= ResolveUrl("~/Scripts/PrintIndustry.js?v=20261113a") %>" defer></script>

    <%-- Regole essenziali del pannello, indipendenti dalla cache di site.css. --%>
    <style>
        #loadingPanel[hidden] {
            display: none !important;
        }

        #loadingPanel:not([hidden]) {
            position: fixed !important;
            box-sizing: border-box !important;
            margin: 0 !important;
            inset: 0 !important;
            z-index: 99999 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
            width: 100vw !important;
            height: 100vh !important;
            background: rgba(18, 32, 52, 0.48) !important;
            padding: 20px !important;
        }

        #loadingPanel .loading-card {
            display: flex !important;
            box-sizing: border-box !important;
            flex-direction: column !important;
            align-items: center !important;
            justify-content: center !important;
            gap: 18px !important;
            width: min(380px, 100%) !important;
            padding: 32px 28px !important;
            background: #ffffff !important;
            color: #173351 !important;
            border: 1px solid #dfe6ef !important;
            border-radius: 16px !important;
            box-shadow: 0 18px 48px rgba(12, 30, 53, .22) !important;
            font: 600 15px/1.5 'Segoe UI', Arial, sans-serif !important;
            text-align: center !important;
        }

        #loadingPanel .loading-spinner {
            display: block !important;
            flex: 0 0 auto !important;
            width: 44px !important;
            height: 44px !important;
            border: 4px solid #e4ebf3 !important;
            border-top-color: #325979 !important;
            border-radius: 50% !important;
            animation: printindustry-spin .75s linear infinite !important;
        }

        @keyframes printindustry-spin {
            to { transform: rotate(360deg); }
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <header>
            <h1>PrintIndustry <span>Monitor</span></h1>
            <nav>
                <button type="button" class="nav active" data-tab="machines">1. Configurazione macchine</button>
                <button type="button" class="nav" data-tab="production">2. Dati produzione</button>
            </nav>
        </header>

        <main>
            <p id="message" class="alert" role="status"></p>

            <section id="machines" class="tab">
                <h2>Configurazione macchinari</h2>
                <p>
                    Imposta i parametri economici delle macchine e salva la configurazione.
                    I tracciati CSV e XML sono letti dalle cartelle in <code>App_Data/Imports</code>.
                    I valori economici iniziali sono stime modificabili.
                </p>

                <div id="machineForms"></div>

                <div class="page-actions">
                    <button type="button" id="goData">Vai ai dati di produzione</button>
                </div>
            </section>

            <section id="production" class="tab" hidden>
                <h2>
                    Industria 4.0
                    <span class="section-subtitle" id="selectedMachineTitle"></span>
                </h2>

                <div class="work-layout">
                    <aside class="filters-panel">
                        <h3>Filtri</h3>

                        <label for="machine">Macchinario</label>
                        <select class="machineFilter" id="machine"></select>

                        <label for="jobFilter">Nome lavorazione</label>
                        <input type="search" id="jobFilter" placeholder="Cerca un lavoro..." autocomplete="off" />

                        <label for="statusFilter">Stato</label>
                        <select id="statusFilter">
                            <option value="">Tutti gli stati</option>
                        </select>

                        <label for="fromDate">Dal</label>
                        <div class="date-field">
                            <input type="date" id="fromDate" />
                            <button type="button" class="date-clear" id="clearFrom" aria-label="Cancella data iniziale">×</button>
                        </div>

                        <label for="toDate">Al</label>
                        <div class="date-field">
                            <input type="date" id="toDate" />
                            <button type="button" class="date-clear" id="clearTo" aria-label="Cancella data finale">×</button>
                        </div>

                        <button type="button" id="resetFilters" class="btn-light reset-button">Resetta filtri</button>
                    </aside>

                    <div class="work-main">
                        <div class="work-head">
                            <div class="work-title" id="machineWorkTitle">Lavorazioni macchina</div>
                            <button type="button" id="sourceAction">Importa dati reali</button>
                        </div>

                        <div class="origin-tabs" role="group" aria-label="Origine delle lavorazioni">
                            <button type="button" id="sourceTabReal" class="origin-tab" aria-pressed="false">Dati reali</button>
                            <button type="button" id="sourceTabSimulated" class="origin-tab" aria-pressed="false">Dati simulati</button>
                        </div>

                        <div class="work-sections">
                            <button type="button" class="work-tab selected" id="workTabJobs">Lavorazioni</button>
                            <button type="button" class="work-tab" id="workTabStats">Statistiche</button>
                        </div>

                        <div id="jobsPanel">
                            <div class="tablewrap">
                                <table>
                                    <thead id="productionHead"></thead>
                                    <tbody id="productionRows">
                                        <tr><td>Caricamento...</td></tr>
                                    </tbody>
                                </table>
                            </div>

                            <div class="production-pager" id="paging">
                                <div class="pager-controls">
                                    <button type="button" class="pager-arrow" id="firstPage" title="Prima pagina" aria-label="Prima pagina">&#171;</button>
                                    <button type="button" class="pager-arrow" id="prevPage" title="Pagina precedente" aria-label="Pagina precedente">&#8249;</button>
                                    <span class="pager-current" id="currentPage">1</span>
                                    <button type="button" class="pager-arrow" id="nextPage" title="Pagina successiva" aria-label="Pagina successiva">&#8250;</button>
                                    <button type="button" class="pager-arrow" id="lastPage" title="Ultima pagina" aria-label="Ultima pagina">&#187;</button>

                                    <label class="pager-size" for="pageSize">
                                        Numero di elementi
                                        <select id="pageSize">
                                            <option value="10" selected>10</option>
                                            <option value="25">25</option>
                                            <option value="50">50</option>
                                            <option value="100">100</option>
                                        </select>
                                    </label>
                                </div>

                                <div class="pager-info" id="pageInfo">Pagina 1 di 1, elementi da 0 a 0 di 0.</div>
                            </div>
                        </div>

                        <div id="statsPanel" hidden>
                            <div id="machineDashboards">
                                <p>Seleziona Statistiche per visualizzare i KPI.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </section>

            <footer>Prototipo accademico · ASP.NET Web Forms · Web API 2 · SQLite · Dati reali e simulati distinti</footer>
        </main>

    </form>
    <%-- Overlay indipendente dal form Web Forms: nessuna riga aggiunta al layout. --%>
    <div id="loadingPanel" class="loading-backdrop" hidden tabindex="-1"
         role="status" aria-live="polite" aria-label="Operazione in corso">
        <div class="loading-card">
            <span class="loading-spinner" aria-hidden="true"></span>
            <span id="loadingPanelText">Operazione in corso...</span>
        </div>
    </div>

</body>
</html>
