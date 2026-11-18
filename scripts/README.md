# Script di supporto

Gli script sono separati in base al loro ruolo nell'esperimento corrente.

## `current/`

Contiene verifiche pertinenti al server MCP e alla toolchain attualmente usati:

- `Invoke-AgentWorkspaceMcpServerProbe.ps1`: verifica diretta del server MCP;
  la modalità `Claude` o `All` avvia anche una sessione LLM reale e richiede
  autorizzazione esplicita;
- `Invoke-McpPublicTestsProbe.ps1`: prova end-to-end dell'esecuzione controllata
  dei test pubblici;
- `New-FrozenSourceManifest.ps1`: estrae direttamente da un commit gli input
  versionati del freeze e ne genera il manifest deterministico SHA-256;
- `New-FrozenHarness.ps1`: pubblica due volte runner e server MCP dal commit
  congelato, ne verifica l'identità byte-per-byte e crea l'harness in sola
  lettura con manifest SHA-256;
- `New-FrozenNodeToolchain.ps1`: esegue due installazioni `npm ci` dal lockfile
  congelato e crea la toolchain Node in sola lettura con manifest SHA-256;
- `Test-OfficialAudit.cjs`: controlla in sola lettura coerenza strutturale e
  hash delle evidenze di un registro delle run rispetto al piano ufficiale;
  contratto e limiti sono documentati in P38;
- `Save-OfficialAudit.cjs`: crea snapshot esclusivi del registro con una catena
  di hash e un anchor esterno; contratto e limiti sono documentati in P39;
- `Probe-AgentTestConfinement.cjs`: verifica con una sentinella sintetica se i
  test agente possono leggere file esterni; interpretazione e limiti in P43.

Il fatto che un singolo probe sia superato certifica soltanto le proprietà
osservate da quella prova, non l'intero isolamento dell'harness.

## `legacy-isolation/`

Contiene script e configurazioni delle precedenti iterazioni basate su Windows
Sandbox e Windows AppContainer. Sono conservati per riprodurre le evidenze
storiche, ma non sono prerequisiti delle nuove run ufficiali.

La documentazione delle relative iterazioni è in
[`docs/benchmark/implementation-log/`](../docs/benchmark/implementation-log/);
i sorgenti ausiliari sono in
[`experiments/legacy-isolation-probes/`](../experiments/legacy-isolation-probes/).
