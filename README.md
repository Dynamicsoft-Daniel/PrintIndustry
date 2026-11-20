# PrintIndustry Monitor

Piattaforma web per il monitoraggio economico-operativo della produzione di Loretoprint srl:
stampa di grande formato con la Canon Colorado 1650 e taglio digitale con la Zünd G3 3XL-1600.

Project Work PW30 · Corso di laurea in Informatica per le Aziende Digitali (L-31) · Daniel Della Puzza

---

## Cosa fa

- **Importa i dati reali** delle due macchine: i CSV prodotti dalla Colorado e gli XML dei
  lavori della Zünd, letti dalle cartelle `App_Data/Imports/*/Reali` e salvati senza duplicati.
- **Genera dati simulati** nello stesso formato dei file reali, per provare la piattaforma su periodi
  e scenari diversi; i dati simulati restano sempre separati da quelli reali.
- **Calcola i costi di ogni lavorazione**: macchina, operatore, energia stimata, materiale e inchiostri
  (Colorado) o utensile (Zünd), con le tariffe in vigore al momento dell'importazione.
- **Mostra gli indicatori in una dashboard**: lavorazioni riuscite, m² prodotti, ore macchina,
  produttività, saturazione e inattività (Colorado), interruzioni (Zünd), scarto, costo totale,
  costo per m², energia per m², con grafici sull'andamento nel tempo e sulla ripartizione dei costi.
- **Espone tutto tramite API REST** in JSON (11 endpoint), usate dalla dashboard e interrogabili
  anche da altri strumenti.

## Stack

- **Back-end**: C# su .NET Framework 4.8, ASP.NET Web API 2, Newtonsoft.Json
- **Database**: SQLite (System.Data.SQLite), accesso con ADO.NET e query parametrizzate
- **Front-end**: ASP.NET Web Forms, jQuery, Bootstrap 5, Chart.js, Bootbox
- **Ambiente**: Visual Studio 2022, IIS Express

## Cosa installare

| Componente | Note |
|---|---|
| Windows 10 o 11 | Il progetto usa .NET Framework, disponibile solo su Windows |
| Visual Studio 2022 o successivo | Va bene l'edizione Community, gratuita. Nel programma di installazione selezionare il carico di lavoro **Sviluppo ASP.NET e Web**, che comprende IIS Express |
| .NET Framework 4.8 (targeting pack) | Dal programma di installazione di Visual Studio, scheda *Singoli componenti*, se non è già presente |
| Browser aggiornato con Internet | Chart.js e Bootbox sono caricati da CDN; jQuery e Bootstrap sono inclusi nel progetto |
| Git (facoltativo) | Solo per clonare il repository; in alternativa si scarica lo ZIP |

Non serve installare un database: SQLite arriva come pacchetto NuGet e il file del database viene
creato da solo al primo avvio. I pacchetti NuGet vengono scaricati automaticamente da Visual Studio
alla prima compilazione.

## Setup rapido

```bash
# 1. Scarica il progetto in una cartella con un percorso breve
git clone https://github.com/Dynamicsoft-Daniel/PrintIndustry.git C:\PrintIndustry
#    (oppure: Code → Download ZIP ed estrai in C:\PrintIndustry)

# 2. Apri C:\PrintIndustry\PrintIndustry.sln con Visual Studio

# 3. Compila: Compila → Compila soluzione (ripristina anche i pacchetti NuGet)

# 4. Avvia con F5 (IIS Express): si apre la dashboard su http://localhost:50781/
```

Il file `App_Data/production.sqlite` e le cartelle mancanti si creano automaticamente alla prima esecuzione.
Per una verifica rapida, `http://localhost:50781/api/machines` deve restituire in JSON le due
macchine; se IIS Express usa un'altra porta, vale quella effettiva.

**Primo utilizzo**: scegliere la macchina, aprire *Dati produzione → Dati reali* e premere
*Importa dati reali*; poi aprire *Statistiche*. Per i dati simulati: *Dati simulati → Genera dati
simulati*.

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
- `App_Data/Imports` — file di produzione reali e simulati delle due macchine.
- `docs` — `schema.sql` (tabelle e indici del database) e `TEST.md` (prove funzionali).

## Risoluzione dei problemi

- **HTTP 500 dopo una *Ricompila***: finché l'app è in esecuzione `SQLite.Interop.dll` (in `bin\x86` e
  `bin\x64`) resta bloccata e la pulizia di `bin` la cancella. Fermare IIS Express, poi usare
  *Compila* (non *Ricompila*) e riavviare.
- **Errori su percorsi troppo lunghi** durante l'estrazione o l'importazione: spostare il progetto in
  una cartella con percorso breve, per esempio `C:\PrintIndustry`.
- **Grafici o finestre di dialogo non visibili**: verificare la connessione Internet, necessaria per
  caricare Chart.js e Bootbox da CDN.

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
