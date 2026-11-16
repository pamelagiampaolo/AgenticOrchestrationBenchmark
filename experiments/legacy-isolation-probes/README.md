# Probe storici di isolamento

Questa directory conserva prototipi ed evidenze tecniche delle precedenti
iterazioni dedicate a Windows Sandbox e Windows AppContainer.

Il materiale non appartiene all'architettura operativa corrente e non è un
prerequisito delle nuove run. Gli script restano disponibili per consentire la
riproduzione delle singole prove storiche documentate in
[`docs/benchmark/`](../../docs/benchmark/).

## Contenuto

- `AppContainerIsolationProbe/`: controllore .NET delle prove P11 e P12 con
  Windows AppContainer;
- `McpSandboxBridgeProbe/`: fixture usate dalla prova P3 del bridge MCP-Windows
  Sandbox;
- `NodeBaselineSandboxProbe/`: script guest della baseline Node.js P2.2;
- `P21IsolationProbe/`: sorgenti e script guest della prova di isolamento P2.1.

La conservazione di questi probe documenta l'evoluzione dell'harness, ma non
costituisce una prova dell'isolamento MCP corrente. Tale proprietà deve essere
verificata separatamente con prove di accettazione sull'harness effettivamente
usato per l'esperimento.
