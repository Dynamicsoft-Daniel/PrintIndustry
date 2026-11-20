# Prove manuali dell'applicazione

Prove da eseguire su Windows con Visual Studio 2022 e IIS Express, dopo aver ripristinato
i pacchetti NuGet e compilato la soluzione.

## Preparazione

- Per una prova da zero, spostare o rinominare `App_Data/production.sqlite`
  (fermare prima IIS Express).
- I file reali sono già presenti nelle cartelle `App_Data/Imports/ZUND_G3/Reali` (XML)
  e `App_Data/Imports/COLORADO_1650/Reali` (CSV).

## Elenco delle prove

| # | Prova | Risultato atteso |
|---|---|---|
| 1 | Avviare l'applicazione | Si apre la sezione **Configurazione macchine**; vengono creati `production.sqlite` e le cartelle `Imports` |
| 2 | Aprire `api/machines` nel browser | JSON con le due macchine: 1 Zünd G3, 2 Canon Colorado 1650 |
| 3 | Modificare e salvare le tariffe di Zünd G3 e Canon Colorado 1650 | Messaggio di conferma; per la Zünd non compaiono i prezzi degli inchiostri, per la Colorado non compare l'utensile |
| 4 | **Dati produzione** → macchina → **Dati reali** → **Importa dati reali** | Pannello di attesa, poi riepilogo con file letti, lavorazioni inserite, errori e durata |
| 5 | Ripetere l'importazione | 0 lavorazioni inserite, nessun duplicato |
| 6 | **Dati simulati** → **Genera dati simulati** → conferma | Nuovo lotto di 30 giorni lavorativi; i conteggi delle due schede restano separati |
| 7 | Generare una seconda volta | Si aggiunge un altro lotto, con identificativi diversi |
| 8 | Filtrare per nome, stato e date; poi **Resetta filtri** | La tabella mostra solo le righe corrispondenti; il reset torna all'elenco completo |
| 9 | Cambiare il numero di righe e la pagina | Paginazione coerente con il totale indicato |
| 10 | Aprire **Statistiche** | Schede KPI (produttività, interruzioni o saturazione, scarto, costi, costo dei fermi, energia) e grafici coerenti con i filtri; andamento con lavorazioni e costo; per la Colorado anche inchiostri e materiali |
| 11 | Cambiare macchina mentre i dati si caricano | Tabella e grafici si svuotano; non compaiono righe dell'altra macchina |
| 12 | Annotare il costo totale, cambiare una tariffa e riaprire le statistiche | Il costo delle lavorazioni già importate non cambia |
| 13 | Chiudere un popup con la **×** in alto a destra | Il popup si chiude |
| 14 | Chiamare `api/production/kpi` senza `machine`, con `machine=99` e con `origine=XYZ` | HTTP 400 con messaggio, in tutti e tre i casi |

Al termine della prova 12 ripristinare la tariffa originale.

## Esito del collaudo del 12/11/2026

Eseguito su Windows 11 con i dati reali dell'azienda (7.583 XML Zünd e 373 CSV Colorado),
partendo da un database vuoto.

- Compilazione con MSBuild: 0 errori, 1 avviso (versione di Newtonsoft.Json, senza effetti in esecuzione).
- Prove 1–14 superate. Importazione Zünd: 7.583 lavorazioni in circa 2,7 s; Colorado: 2.654 lavorazioni
  in circa 0,3 s; seconda importazione senza nuove righe.
- Prova 12: tariffa Zünd portata da 35 a 40 €/h, costo totale dei dati reali invariato (70.761 €).
- Osservazione sui dati: 118 dei 373 CSV della Colorado non contengono lavorazioni nuove (copie di
  altri file o file vuoti). Non vengono inseriti due volte e dal 13/11/2026 il riepilogo li indica
  a parte come "senza nuove lavorazioni".

## Correzioni del 13/11/2026

- `kpi` con una macchina inesistente rispondeva HTTP 500: ora HTTP 400 "Macchinario non valido".
- `trend`, `adaptive-trend`, `consumptions`, `source-counts` e `simulate` senza `machine` rispondevano
  404 o 411: ora HTTP 400 come le altre API.
- `origine` diverso da `REALE`/`SIMULATO` veniva accettato e restituiva zeri: ora HTTP 400.
- Il riepilogo dell'importazione distingue i file già importati da quelli senza nuove lavorazioni.
- La nota dei KPI Zünd per i dati simulati indica che gli scarti sono generati dal simulatore.
- Verifica: compilazione senza errori, API provate su IIS Express, importazione
  Colorado da database vuoto (255 file con dati nuovi, 118 senza nuove lavorazioni, 2.654 lavorazioni)
  e seconda importazione (373 già importati, 0 lavorazioni).

## Revisione del codice del 13/11/2026

- Eliminato il codice non utilizzato e le colonne `EstimatedSeconds` / `RipSeconds`, mai valorizzate.
- Riformattati parser, andamento adattivo e accesso ai dati, senza modifiche alla logica: le stringhe SQL
  sono rimaste identiche e la reimportazione da database vuoto ha prodotto le stesse 10.237 lavorazioni
  reali (stesse chiavi, stessi campi e costi) del database originale.
- Namespace allineati alle cartelle (`PrintIndustry.Models`, `PrintIndustry.DAL`, `PrintIndustry.Controllers`);
  le classi `Ingestion` e `Analysis` sono diventate `Controller_Importazione` e `Controller_Statistiche`.
- Nelle API gli errori non previsti vengono registrati nel log e restituiscono HTTP 500 con un messaggio
  generico. Prova: database reso illeggibile con l'applicazione avviata → tutte le API rispondono 500 con
  il messaggio generico, senza testo dell'eccezione.

## Nuovi indicatori e collaudo ripetuto (13/11/2026)

Per coprire tutti gli indicatori richiesti dalla traccia del PW30:

- la dashboard mostra anche saturazione e inattività (Colorado), tasso di scarto, costo per m², costo
  stimato dei fermi ed energia per m²; quando un valore non è disponibile la scheda indica "n.d.";
- il grafico dell'andamento mostra lavorazioni e costo stimato per periodo (due assi); il tooltip riporta
  anche superficie, ore ed energia;
- il simulatore Colorado genera fermi (circa un lavoro su quattro, da 1 a 15 minuti) e lavori non
  riusciti (circa il 6%, stati `Abrt` e `Stop`).

Collaudo ripetuto da database vuoto (database e file simulati precedenti conservati in
`App_Data/production.backup-2026-11-12.sqlite` e `App_Data/Imports/_backup_20261112/`):

- importazione Zünd 7.583 lavorazioni in 2,80 s; Colorado 2.654 lavorazioni in 0,33 s (255 file con dati
  nuovi, 118 senza nuove lavorazioni); seconda importazione senza nuove righe;
- `simulate` con `seed=42`: Zünd 134 lavorazioni, Colorado 30 file e 157 righe; due generazioni senza seme
  per macchina portano i simulati a 401 (Zünd) e 466 (Colorado);
- KPI dei dati reali invariati (Zünd 70.761 €, Colorado 78.092 €); dati simulati: scarto Zünd 1,23%
  (34 pezzi), Colorado 22 lavori non riusciti e costo stimato dei fermi di circa 899 €;
- prova 12 ripetuta: tariffa Zünd da 35 a 40 €/h e ritorno, costo totale dei dati reali invariato;
- layout delle statistiche verificato a 1600 e 390 px, senza scorrimento orizzontale;
