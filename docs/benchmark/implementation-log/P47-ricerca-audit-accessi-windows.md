# P47 — Ricerca sull'audit degli accessi ai file Windows

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** ricerca di una verifica post-run degli accessi.
- **Stato:** documentazione Microsoft consultata; configurazione effettiva
  della policy e SACL riservata non accertata per mancanza di privilegi.
- **Chiamate LLM sperimentali:** nessuna; nessun contenuto riservato letto.

Obiettivo: stabilire se Windows possa registrare letture e se tali registri
siano utilizzabili per controllare le run. Componenti: audit del filesystem,
Security log, ACL di audit della directory riservata e configurazione locale.
Criteri: distinguere capacità del sistema, configurazione realmente osservata,
copertura dimostrata e limiti del controllo a posteriori.

## Ricerca documentale

Secondo [Microsoft — Audit File System](https://learn.microsoft.com/windows/security/threat-protection/auditing/audit-file-system),
gli eventi dipendono sia dalla policy di audit sia dalle SACL degli oggetti e
dalla corrispondenza con l'account e il tipo di accesso; le SACL predefinite
non generano eventi di audit del filesystem. L'evento
[4663](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/event-4663)
registra l'uso di un diritto su un oggetto, quando la SACL corrisponde, e può
includere percorso, account e informazioni sul processo. La richiesta di un
handle è distinta dall'uso effettivo del diritto; l'evento 4663 non ha eventi
di fallimento. Il controllo richiede una configurazione appropriata e una prova
positiva, non basta l'assenza di eventi trovati.

La [documentazione Microsoft sulla policy degli eventi](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gpsb/0b9673a7-ce0a-49b4-912b-591efdb37cdf)
descrive anche modalità di conservazione che possono sovrascrivere eventi.
La registrazione è una rilevazione a posteriori: non impedisce di per sé che un
processo legga i file, non identifica il contenuto letto e non certifica
l'assenza di altri percorsi o copie non soggetti all'audit.

## Verifiche locali in sola lettura

- `auditpol.exe /get '/subcategory:{0CCE921D-69AE-11D9-BED3-505054503030}' /r`:
  errore `0x00000522`, privilegio non posseduto, exit code 1314, anche con
  accesso autorizzato ai normali permessi dell'utente. Policy non accertata.
- `Get-Acl -Audit` sulla directory `reserved`: privilegio
  `SeSecurityPrivilege` assente, anche con accesso autorizzato. SACL non letta;
  non si conclude che sia presente o assente.
- `wevtutil.exe gl Security`: exit code 0, canale Security abilitato,
  `retention: false`, `autoBackup: false`, `maxSize: 20971520` byte. Canale
  abilitato non implica che l'audit del filesystem sia abilitato o completo.
- Una richiesta `Get-WinEvent` per eventi 4663 nelle 12 ore precedenti ha
  restituito `Impossibile trovare eventi che soddisfino i criteri`; senza policy
  e SACL verificabili non è prova dell'assenza di accessi né della copertura del
  periodo dei piloti.

Non sono state cambiate policy, SACL, ACL, gruppi o configurazioni del log.
Non sono stati aperti o mostrati contenuti dei test riservati.

## Implicazioni e lavoro rimanente

È possibile progettare un audit mirato delle cartelle e delle copie riservate
con controllo umano o automatico dei log dopo la run. Per considerarlo
affidabile servono privilegi per leggere e configurare policy e SACL, prova
positiva con lettura di sentinella da processo equivalente, controllo dei figli,
copertura di tutti i percorsi e copie rilevanti, conservazione del log e
correlazione temporale. L'overhead reale e la durata della configurazione non
sono stati misurati. Non è possibile verificare retroattivamente i piloti in
assenza di prova che la registrazione fosse già attiva e completa.

Il requisito corrente del repository prescrive che gli agenti non possano
leggere o modificare i test riservati e richiede prove di isolamento; la sola
rilevazione dopo la lettura non soddisfa tale requisito senza una revisione
esplicita del protocollo. Nessuna decisione architetturale o modifica di sistema
è stata fatta, e questa ricognizione non richiede di ripetere i piloti.
