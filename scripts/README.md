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
  versionati del freeze e ne genera il manifest deterministico SHA-256.

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
