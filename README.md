# PrintIndustry Monitor — PW30

Applicazione web per il monitoraggio economico-operativo del reparto di stampa e taglio di
Loretoprint srl (Canon Colorado 1650 e Zünd G3 3XL-1600).
ASP.NET Web Forms su .NET Framework 4.8, ASP.NET Web API 2 e SQLite, in un unico progetto web
**PrintIndustry** sviluppato con Visual Studio 2022.

## Requisiti

- Visual Studio 2022 con il carico di lavoro *Sviluppo ASP.NET e Web* e il targeting pack .NET Framework 4.8.
- Connessione Internet dal browser: Bootbox (cdnjs) e Chart.js (jsDelivr) sono caricati da CDN.
  jQuery 3.6.1 e Bootstrap 5.3.6 sono inclusi in locale.

## Avvio

1. Aprire `PrintIndustry.sln` e ripristinare i pacchetti NuGet
   (Microsoft.AspNet.WebApi.WebHost 5.3.0, Newtonsoft.Json 13.0.3, System.Data.SQLite.Core 1.0.118).
2. Impostare `PrintIndustry` come progetto di avvio e avviare con IIS Express
   (indirizzo predefinito `http://localhost:50781/`).
3. Al primo avvio vengono creati `App_Data/production.sqlite` e le cartelle `App_Data/Imports`.
4. Verifica rapida: `http://localhost:50781/api/machines` deve restituire in JSON le due macchine.
   Se IIS Express usa un'altra porta, usare quella effettiva.

Prima di una *Ricompila* da Visual Studio o da MSBuild conviene fermare IIS Express: finché l'app
è in esecuzione `SQLite.Interop.dll` (in `bin\x86` e `bin\x64`) resta bloccata e la pulizia di `bin` la cancella,
con errore HTTP 500 al riavvio successivo.

## Uso dell'applicazione

1. **Configurazione macchine** — impostare per ogni macchina le tariffe (costo orario di macchina e
   operatore, potenza, energia, materiale, inchiostri per la Colorado, utensile per la Zünd) e premere
   *Salva*. I valori iniziali sono ipotesi dimostrative.
2. **Dati produzione → Dati reali** — scegliere la macchina e premere *Importa dati reali*. Vengono
   letti i file della cartella `Reali` della macchina; alla fine un riepilogo indica i file con dati
   nuovi, quelli già importati, quelli senza nuove lavorazioni (copie o file vuoti), gli errori, le
   lavorazioni inserite e la durata. Ripetendo l'importazione non si creano duplicati.
3. **Dati produzione → Dati simulati** — premere *Genera dati simulati* e confermare: viene creato un
   nuovo lotto di 30 giorni lavorativi, salvato separato dai dati reali.
4. **Lavorazioni** — tabella paginata (10, 25, 50 o 100 righe) con filtri per nome, stato e
   intervallo di date; *Resetta filtri* torna all'elenco completo.
5. **Statistiche** — schede con lavorazioni riuscite, superficie, tempo macchina, produttività,
   interruzioni (Zünd) o saturazione e inattività (Colorado), tasso di scarto, costo totale, costo per m²,
   costo stimato dei fermi, energia e energia per m²; quattro grafici: ripartizione dei costi, andamento
   di lavorazioni e costi nel tempo e, per la Colorado, consumi di inchiostro e di materiale. Indicatori
   e grafici rispettano gli stessi filtri della tabella; un valore non disponibile (per esempio lo scarto
   nei dati reali) è indicato come "n.d.".
6. **Modifica delle tariffe** — le nuove tariffe valgono solo per le importazioni successive: i costi
   delle lavorazioni già salvate non cambiano (vedi *Storicizzazione dei costi*).

All'apertura, o quando si cambia macchina, viene selezionata la scheda *Dati reali* se per quella
macchina esiste almeno un record reale, altrimenti *Dati simulati*.

## Dati reali e simulati

| Cartella | Contenuto |
|---|---|
| `App_Data/Imports/COLORADO_1650/Reali/` | CSV della Canon Colorado (righe di tipo 4302/4303) |
| `App_Data/Imports/ZUND_G3/Reali/` | XML `ZCC_cmd` / `JobStatus` della Zünd G3 |
| `App_Data/Imports/*/Simulati/` | file prodotti dai generatori, nello stesso formato |

- Ogni lavorazione ha il campo `OrigineDato` (`REALE` / `SIMULATO`); elenco, stati, KPI, andamento e
  consumi sono sempre interrogati per macchina **e** origine.
- Il vincolo univoco `(MachineId, OrigineDato, SourceKey)` impedisce i duplicati; `ImportFiles`
  registra nome, dimensione e data dei file già letti, che vengono saltati senza riaprirli.
- I lavori simulati hanno identificativi che iniziano con `SIM`. Senza `seed` ogni generazione crea un
  lotto nuovo; con `seed` esplicito (solo via API) la generazione è ripetibile.
- Il simulatore Zünd genera interruzioni e circa l'8% di lavori con un pezzo scartato; il simulatore
  Colorado genera fermi in circa un lavoro su quattro e circa il 6% di lavori non riusciti (`Abrt`, `Stop`).
- I file reali di Loretoprint srl sono inclusi nel repository, con il consenso dell'azienda: dopo il primo
  avvio basta premere *Importa dati reali*.

## API

Tutte le risposte sono in JSON. Le API di produzione richiedono `machine` (1 = Zünd G3,
2 = Canon Colorado 1650). `origine` vale `REALE` (predefinito) o `SIMULATO`.
Rispondono HTTP 400 con un messaggio se `machine` manca o non esiste, se `origine` ha un altro
valore o se le date non sono valide.

| Metodo | Percorso | Parametri principali |
|---|---|---|
| GET | `api/machines` | — |
| POST | `api/machines/save` | macchina nel corpo della richiesta |
| GET | `api/production` | `machine`, `origine`, `page`, `pageSize`, `status`, `job`, `from`, `to` |
| GET | `api/production/statuses` | `machine`, `origine` |
| GET | `api/production/kpi` | `machine`, `origine`, `status`, `job`, `from`, `to` |
| GET | `api/production/trend`, `api/production/adaptive-trend` | come `kpi` |
| GET | `api/production/consumptions` | come `kpi` (inchiostri e materiali Colorado) |
| GET | `api/production/source-counts` | `machine` → `{"Reali":n,"Simulati":n}` |
| POST | `api/production/import` | `machine` (query string) |
| POST | `api/production/simulate` | `machine`, `days` (1–365, predefinito 30), `seed` facoltativo |

`adaptive-trend` sceglie il raggruppamento in base all'intervallo: giorni fino a 30 giorni, blocchi di
15 giorni fino a 60, mesi oltre, anni oltre i 12 mesi; senza filtri mostra gli ultimi 12 mesi. Per ogni
periodo restituisce lavorazioni, lavorazioni riuscite, ore, superficie, energia e costo stimato.

## Test

Le prove funzionali sono descritte in `docs/TEST.md` (14 prove manuali sull'applicazione avviata con
IIS Express: configurazione, importazione e reimportazione, generazione dei dati simulati, filtri,
statistiche, cambio di macchina, costi dopo il cambio di tariffa, errori delle API).

Collaudo eseguito su Windows 11 il 12/11/2026 e ripetuto da database vuoto il 13/11/2026: compilazione
con MSBuild (0 errori, 1 avviso per la versione di Newtonsoft.Json), prove 1–14 superate, API
verificate su IIS Express con i dati reali (7.583 XML Zünd e 373 CSV Colorado) e con dati simulati.
Esiti in `docs/TEST.md`.

## Architettura

- `Views` — pagina `Dashboard.aspx` con code-behind e designer (`DependentUpon`).
- `Scripts/PrintIndustry.js` — JavaScript tradizionale: variabili `var`, funzioni con nome, chiamate
  `$.ajax` con callback separate, nessuna arrow function, `let`, `const` o IIFE.
  Le chiamate usano il percorso dell'applicazione generato con `ResolveUrl("~/")`.
- `Api` (`PrintIndustry.Api`) — controller Web API 2 che ricevono le richieste e richiamano `Controllers`.
  Un errore sui dati della richiesta risponde HTTP 400 con il messaggio; un errore non previsto viene
  scritto nel log (`Trace`) e risponde HTTP 500 con un messaggio generico, senza dettagli interni.
- `Controllers` (`PrintIndustry.Controllers`) — `Controller_Produzione` (punto d'ingresso delle API),
  `Controller_Importazione`, `Controller_Statistiche` (KPI), `Controller_Costi`, `Controller_Percorsi`,
  parser (`Controller_Parser.cs`) e simulatori (`ZundSimulator`, `ColoradoSimulator`).
- `DAL` (`PrintIndustry.DAL`) — classe `Db`, accesso a SQLite con ADO.NET e query parametrizzate.
- `Models` (`PrintIndustry.Models`) — classi dati.

## Storicizzazione dei costi

Insieme a ogni nuova riga `Production`, `Controller_Costi` salva le tariffe usate (costi orari,
potenza, energia, materiale, utensile Zünd, prezzi CMYK Colorado), i singoli costi e il totale.
KPI e grafici sommano i costi già salvati e non rimoltiplicano le quantità per le tariffe attuali.
`CostCapturedAt` registra la data del listino e `CostSnapshotSource` vale `IMPORTAZIONE` o `MIGRAZIONE`.

- Il listino è associato al **momento dell'importazione**, non alla data di produzione scritta nel file.
- Per i record precedenti a questa funzione la migrazione ha congelato una sola volta il listino
  disponibile (`MIGRAZIONE`): non sono prezzi realmente applicati in passato.
- Una reimportazione non sovrascrive mai i costi già salvati.
- Su un database creato da versioni precedenti la colonna `OrigineDato` viene aggiunta con valore
  `REALE`: conviene farne una copia prima dell'avvio.

## Importazione

Per ogni macchina: una sola connessione SQLite, lettura iniziale del registro `ImportFiles`, comandi
SQL riutilizzati e commit ogni 300 file. Ogni file viene inserito in un savepoint, quindi un file
danneggiato viene registrato come errore senza far perdere gli altri. I percorsi lunghi di Windows
(OneDrive) sono gestiti con la sintassi estesa dei percorsi. Durante importazione e generazione un
pannello di attesa blocca i comandi e si chiude anche in caso di errore o timeout.

## Unità e limiti

- Energia stimata con una potenza di riferimento modificabile: 2,3 kW in stampa per la Colorado,
  3,6 kW per la Zünd senza aspirazione. Non sono kWh misurati.
- I fattori di conversione delle unità dei CSV Canon sono ricavati dai campioni e verificati con
  l'azienda sui lavori eseguiti; l'area Zünd è quella geometrica dei lavori, non il materiale consumato.
- Lo scarto reale non è nei file; lo scarto simulato è tenuto distinto.
- L'inattività della Colorado è stimata rispetto alle ore disponibili configurate.
- Non ci sono autenticazione, profili utente e backup.
- Un file sostituito con contenuto diverso ma stesso nome, dimensione e data non viene rilevato.

## Revisioni e documentazione

| Data | Revisione |
|---|---|
| 30/10/2026 | Separazione dati reali / simulati |
| 03/11/2026 | Filtro macchina obbligatorio, storicizzazione dei costi, pannello di attesa, importazione ottimizzata |
| 04/11/2026 | Generazione ripetuta e layout su schermi larghi |
| 05/11/2026 | Importazione di oltre 7.000 file e percorsi lunghi |
| 12/11/2026 | Collaudo su Windows e pulsante di chiusura dei popup |
| 13/11/2026 | Controllo di macchina e origine nelle API, riepilogo dei file senza nuove lavorazioni, revisione del codice, nuovi KPI in dashboard e fermi nel simulatore Colorado |

Documenti: `docs/schema.sql` (schema del database: tabelle e indici) e `docs/TEST.md` (prove funzionali).
