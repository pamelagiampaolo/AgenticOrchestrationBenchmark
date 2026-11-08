using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace AppContainerIsolationProbe;

internal static partial class Program
{
    private static int RunNodeController(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException("Uso: --node-controller <baseline> <node.exe> <run-root>.");
        }

        string baselineRoot = Path.GetFullPath(args[1]);
        string sourceNodePath = Path.GetFullPath(args[2]);
        string runRoot = Path.GetFullPath(args[3]);
        string workerRoot = string.Empty;
        string junctionPath = string.Empty;
        string forbiddenRoot = Path.Combine(runRoot, "forbidden");
        string evidenceRoot = Path.Combine(runRoot, "evidence");
        string forbiddenSentinel = Path.Combine(forbiddenRoot, "forbidden.txt");
        string forbiddenWrite = Path.Combine(forbiddenRoot, "blocked.txt");
        string evidencePath = Path.Combine(evidenceRoot, "p12-results.json");
        string profileName = $"TesiAgenticAI.P12.{Guid.NewGuid():N}";
        IntPtr appContainerSid = IntPtr.Zero;
        bool profileCreated = false;

        Directory.CreateDirectory(runRoot);
        string baselineHashBefore = ComputeBaselineHash(baselineRoot);
        try
        {
            int createResult = NativeMethods.CreateAppContainerProfile(
                profileName,
                profileName,
                "Temporary P12 Node worker probe",
                IntPtr.Zero,
                0,
                out appContainerSid);
            if (createResult != 0) Marshal.ThrowExceptionForHR(createResult);
            profileCreated = true;

            string sidValue = ConvertSidToString(appContainerSid);
            string appContainerStorageRoot = GetAppContainerStoragePath(sidValue);
            workerRoot = Path.Combine(appContainerStorageRoot, "LocalState", "p12-worker");
            string projectRoot = Path.Combine(workerRoot, "project");
            string toolchainRoot = Path.Combine(workerRoot, "toolchain");
            string scratchRoot = Path.Combine(workerRoot, "scratch");
            string outputRoot = Path.Combine(workerRoot, "output");
            string profileRoot = Path.Combine(workerRoot, "profile");
            string configRoot = Path.Combine(projectRoot, ".trusted");
            junctionPath = Path.Combine(projectRoot, "external-link");
            string copiedNodePath = Path.Combine(toolchainRoot, "node.exe");
            string nodeProbePath = Path.Combine(configRoot, "node-isolation-probe.mjs");
            string nodeProbeResultPath = Path.Combine(outputRoot, "node-isolation.json");
            string vitestConfigPath = Path.Combine(configRoot, "vitest.public.config.mjs");
            string vitestReportPath = Path.Combine(outputRoot, "public-tests.json");

            CopyDirectory(baselineRoot, projectRoot, excludedDirectoryName: "node_modules");
            CopyDirectory(Path.Combine(baselineRoot, "node_modules"), Path.Combine(projectRoot, "node_modules"));
            Directory.CreateDirectory(toolchainRoot);
            Directory.CreateDirectory(scratchRoot);
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(profileRoot);
            Directory.CreateDirectory(configRoot);
            Directory.CreateDirectory(forbiddenRoot);
            Directory.CreateDirectory(evidenceRoot);
            File.Copy(sourceNodePath, copiedNodePath, overwrite: false);
            File.WriteAllText(forbiddenSentinel, "P12_FORBIDDEN_SENTINEL", Encoding.UTF8);
            File.WriteAllText(vitestConfigPath, CreateVitestConfiguration(), Encoding.UTF8);
            File.WriteAllText(nodeProbePath, NodeProbeSource, Encoding.UTF8);
            CreateJunction(junctionPath, forbiddenRoot);

            var sid = new SecurityIdentifier(sidValue);
            GrantDirectoryAccess(workerRoot, sid, FileSystemRights.Modify | FileSystemRights.ReadAndExecute);

            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["APPDATA"] = profileRoot,
                ["LOCALAPPDATA"] = profileRoot,
                ["USERPROFILE"] = profileRoot,
                ["TEMP"] = scratchRoot,
                ["TMP"] = scratchRoot,
                ["PATH"] = $"{toolchainRoot};{Environment.GetEnvironmentVariable("SYSTEMROOT")}\\System32",
                ["NODE_OPTIONS"] = "--preserve-symlinks --preserve-symlinks-main",
            };

            string typescriptPath = Path.Combine(projectRoot, "node_modules", "typescript", "bin", "tsc");
            string vitestPath = Path.Combine(projectRoot, "node_modules", "vitest", "vitest.mjs");
            ProcessRunResult vitestVersion = RunInAppContainer(
                copiedNodePath,
                ["--preserve-symlinks", "--preserve-symlinks-main", vitestPath, "--version"],
                projectRoot,
                appContainerSid,
                10_000,
                environment,
                Path.Combine(outputRoot, "vitest-version"));
            ProcessRunResult workerThread = RunInAppContainer(
                copiedNodePath,
                [
                    "--eval",
                    "const {Worker}=require('node:worker_threads');const w=new Worker(\"const {parentPort}=require('node:worker_threads');parentPort.postMessage(1)\",{eval:true,stdout:true,stderr:true});w.stdout.resume();w.stderr.resume();w.once('message',()=>process.exit(0));w.once('error',error=>{console.error(error);process.exit(3)});",
                ],
                projectRoot,
                appContainerSid,
                5_000,
                environment,
                Path.Combine(outputRoot, "worker-thread"));
            ProcessRunResult typecheck = RunInAppContainer(
                copiedNodePath,
                ["--preserve-symlinks", "--preserve-symlinks-main", typescriptPath, "--project", Path.Combine(projectRoot, "tsconfig.json"), "--noEmit"],
                projectRoot,
                appContainerSid,
                30_000,
                environment,
                Path.Combine(outputRoot, "typecheck"));
            ProcessRunResult build = RunInAppContainer(
                copiedNodePath,
                ["--preserve-symlinks", "--preserve-symlinks-main", typescriptPath, "--project", Path.Combine(projectRoot, "tsconfig.json")],
                projectRoot,
                appContainerSid,
                30_000,
                environment,
                Path.Combine(outputRoot, "build"));
            ProcessRunResult publicTests = RunInAppContainer(
                copiedNodePath,
                [
                    "--preserve-symlinks", "--preserve-symlinks-main", vitestPath, "run", "--root", projectRoot, "--config", vitestConfigPath,
                    "--configLoader=native", "--reporter=json", $"--outputFile={vitestReportPath}",
                ],
                projectRoot,
                appContainerSid,
                15_000,
                environment,
                Path.Combine(outputRoot, "public-tests"));
            ProcessRunResult dnsIsolation = RunInAppContainer(
                copiedNodePath,
                [
                    "--eval",
                    "require('node:dns').lookup('example.com', error => process.exit(error ? 3 : 0))",
                ],
                projectRoot,
                appContainerSid,
                5_000,
                environment,
                Path.Combine(outputRoot, "dns-isolation"));

            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            using var httpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            httpListener.Start();
            int tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            int httpPort = ((IPEndPoint)httpListener.LocalEndpoint).Port;
            Task httpServer = ServeSingleHttpRequestAsync(httpListener);
            ProcessRunResult tcpIsolation = RunInAppContainer(
                copiedNodePath,
                [
                    "--eval",
                    $"const net=require('node:net');const s=net.createConnection({{host:'127.0.0.1',port:{tcpPort}}});s.on('connect',()=>process.exit(0));s.on('error',()=>process.exit(3));",
                ],
                projectRoot,
                appContainerSid,
                5_000,
                environment,
                Path.Combine(outputRoot, "tcp-isolation"));
            ProcessRunResult httpIsolation = RunInAppContainer(
                copiedNodePath,
                [
                    "--eval",
                    $"const http=require('node:http');const r=http.get('http://127.0.0.1:{httpPort}/probe',x=>process.exit(x.statusCode===200?0:4));r.on('error',()=>process.exit(3));",
                ],
                projectRoot,
                appContainerSid,
                5_000,
                environment,
                Path.Combine(outputRoot, "http-isolation"));
            ProcessRunResult nodeIsolation = RunInAppContainer(
                copiedNodePath,
                [
                    "--preserve-symlinks", "--preserve-symlinks-main", nodeProbePath,
                    "probe",
                    projectRoot,
                    forbiddenSentinel,
                    forbiddenWrite,
                    junctionPath,
                    scratchRoot,
                    nodeProbeResultPath,
                    copiedNodePath,
                ],
                projectRoot,
                appContainerSid,
                30_000,
                environment,
                Path.Combine(outputRoot, "node-isolation"));
            string childProbeResultPath = Path.Combine(outputRoot, "child-isolation.json");
            ProcessRunResult childIsolation = RunInAppContainer(
                copiedNodePath,
                [
                    "--preserve-symlinks", "--preserve-symlinks-main", nodeProbePath,
                    "parent", forbiddenSentinel, childProbeResultPath, copiedNodePath,
                ],
                projectRoot,
                appContainerSid,
                10_000,
                environment,
                Path.Combine(outputRoot, "child-isolation"));
            tcpListener.Stop();
            httpListener.Stop();
            httpServer.Wait(TimeSpan.FromSeconds(1));

            JsonElement? vitestReport = TryReadJson(vitestReportPath);
            JsonElement? nodeReport = TryReadJson(nodeProbeResultPath);
            JsonElement? childReport = TryReadJson(childProbeResultPath);
            var checks = new Dictionary<string, bool>
            {
                ["typecheckSucceeded"] = !typecheck.TimedOut && typecheck.ExitCode == 0,
                ["buildSucceeded"] = !build.TimedOut && build.ExitCode == 0,
                ["publicTestsSucceeded"] = !publicTests.TimedOut && publicTests.ExitCode == 0,
                ["publicTestReportProduced"] = vitestReport.HasValue,
                ["publicTestCountExpected"] = vitestReport?.GetProperty("numTotalTests").GetInt32() == 27,
                ["publicTestsAllPassed"] = vitestReport?.GetProperty("numPassedTests").GetInt32() == 27,
                ["nodeProbeSucceeded"] = !nodeIsolation.TimedOut && nodeIsolation.ExitCode == 0,
                ["nodeReportProduced"] = nodeReport.HasValue,
                ["allowedReadSucceeded"] = nodeReport?.GetProperty("allowedReadSucceeded").GetBoolean() == true,
                ["allowedWriteSucceeded"] = nodeReport?.GetProperty("allowedWriteSucceeded").GetBoolean() == true,
                ["forbiddenReadDenied"] = nodeReport.HasValue && !nodeReport.Value.GetProperty("forbiddenReadSucceeded").GetBoolean(),
                ["forbiddenWriteDenied"] = nodeReport.HasValue && !nodeReport.Value.GetProperty("forbiddenWriteSucceeded").GetBoolean(),
                ["junctionReadDenied"] = nodeReport.HasValue && !nodeReport.Value.GetProperty("junctionReadSucceeded").GetBoolean(),
                ["vitestCliStarted"] = !vitestVersion.TimedOut && vitestVersion.ExitCode == 0,
                ["nodeWorkerThreadStarted"] = !workerThread.TimedOut && workerThread.ExitCode == 0,
                ["childProcessStarted"] = childReport?.GetProperty("started").GetBoolean() == true,
                ["childProcessForbiddenReadDenied"] = childReport.HasValue && !childReport.Value.GetProperty("forbiddenReadSucceeded").GetBoolean(),
                ["dnsDenied"] = dnsIsolation.TimedOut || dnsIsolation.ExitCode != 0,
                ["tcpDenied"] = tcpIsolation.TimedOut || tcpIsolation.ExitCode != 0,
                ["httpDenied"] = httpIsolation.TimedOut || httpIsolation.ExitCode != 0,
                ["environmentAllowlisted"] = nodeReport?.GetProperty("unexpectedEnvironmentNames").GetArrayLength() == 0,
                ["sourceBaselineUnchanged"] = ComputeBaselineHash(baselineRoot) == baselineHashBefore,
            };
            var report = new
            {
                profileName,
                appContainerSid = sidValue,
                nodeVersion = GetNodeVersion(sourceNodePath),
                typecheck,
                build,
                publicTests,
                dnsIsolation,
                tcpIsolation,
                httpIsolation,
                nodeIsolation,
                childIsolation,
                childProbeReport = childReport,
                vitestVersion,
                workerThread,
                publicTestSummary = vitestReport,
                nodeProbeReport = nodeReport,
                checks,
            };
            foreach (string diagnosticFile in Directory.EnumerateFiles(outputRoot, "*.txt"))
            {
                File.Copy(diagnosticFile, Path.Combine(evidenceRoot, Path.GetFileName(diagnosticFile)), overwrite: true);
            }
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
            Console.WriteLine($"RUN_ROOT={runRoot}");
            Console.WriteLine($"EVIDENCE_PATH={evidencePath}");
            return checks.Values.All(value => value) ? 0 : 1;
        }
        finally
        {
            TryDeleteJunction(junctionPath);
            TryDeleteDirectory(workerRoot);
            if (profileCreated)
            {
                int deleteResult = NativeMethods.DeleteAppContainerProfile(profileName);
                if (deleteResult != 0) Console.Error.WriteLine($"Rimozione profilo P12 fallita: 0x{deleteResult:X8}.");
            }
            if (appContainerSid != IntPtr.Zero) NativeMethods.LocalFree(appContainerSid);
            TryDeleteDirectory(forbiddenRoot);
        }
    }

    private static string CreateVitestConfiguration() =>
        """
        export default {
          test: {
            environment: "node",
            pool: "threads",
            maxWorkers: 1,
            fileParallelism: false,
            execArgv: ["--preserve-symlinks", "--preserve-symlinks-main"],
            testTimeout: 5000,
            hookTimeout: 5000,
            include: [
              "tests/public/inventory-management.test.ts",
              "tests/public/order-management.test.ts"
            ],
            exclude: ["tests/reserved/**"]
          }
        };
        """;

    private static void CopyDirectory(string sourceRoot, string destinationRoot, string? excludedDirectoryName = null)
    {
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);
        Directory.CreateDirectory(destinationRoot);
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var info = new DirectoryInfo(directory);
            if (info.LinkTarget is not null) throw new IOException($"Collegamento non autorizzato: {directory}");
            string relative = Path.GetRelativePath(sourceRoot, directory);
            if (excludedDirectoryName is not null && relative.Split(Path.DirectorySeparatorChar).Contains(excludedDirectoryName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }
        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            if (info.LinkTarget is not null) throw new IOException($"Collegamento non autorizzato: {file}");
            string relative = Path.GetRelativePath(sourceRoot, file);
            if (excludedDirectoryName is not null && relative.Split(Path.DirectorySeparatorChar).Contains(excludedDirectoryName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            string destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: false);
        }
    }

    private static JsonElement? TryReadJson(string path)
    {
        if (!File.Exists(path)) return null;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string GetAppContainerStoragePath(string sid)
    {
        int result = NativeMethods.GetAppContainerFolderPath(sid, out IntPtr pathPointer);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
        try
        {
            return Marshal.PtrToStringUni(pathPointer)
                ?? throw new InvalidOperationException("Percorso storage AppContainer non disponibile.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPointer);
        }
    }

    private static string ComputeBaselineHash(string root)
    {
        var builder = new StringBuilder();
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path =>
                     {
                         string relative = Path.GetRelativePath(root, path);
                         string firstSegment = relative.Split(Path.DirectorySeparatorChar)[0];
                         return !string.Equals(firstSegment, "node_modules", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(firstSegment, "dist", StringComparison.OrdinalIgnoreCase);
                     })
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
            builder.Append(hash).Append("  ").Append(relative).Append('\n');
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string GetNodeVersion(string nodePath)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = nodePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            ArgumentList = { "--version" },
        }) ?? throw new InvalidOperationException("Avvio Node per versione non riuscito.");
        string version = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return version;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Pulizia directory P12 fallita per '{path}': {exception.Message}");
        }
    }

    private const string NodeProbeSource = """
        import fs from "node:fs";
        import { spawn } from "node:child_process";

        const mode = process.argv[2];
        const canRead = (path) => { try { fs.readFileSync(path); return true; } catch { return false; } };
        const canWrite = (path) => { try { fs.writeFileSync(path, "PROBE"); return true; } catch { return false; } };

        if (mode === "grandchild") {
          fs.writeFileSync(process.argv[4], JSON.stringify({ forbiddenReadSucceeded: canRead(process.argv[3]) }));
          process.exit(0);
        }

        if (mode === "parent") {
          const [forbiddenFile, childOutput, nodePath] = process.argv.slice(3);
          const grandchildOutput = `${childOutput}.grandchild`;
          const child = spawn(nodePath, ["--preserve-symlinks", "--preserve-symlinks-main", process.argv[1], "grandchild", forbiddenFile, grandchildOutput], { stdio: "ignore" });
          let settled = false;
          const finish = (result) => {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            fs.writeFileSync(childOutput, JSON.stringify(result));
            process.exit(0);
          };
          const timer = setTimeout(() => { child.kill(); finish({ started: true, forbiddenReadSucceeded: true }); }, 5000);
          child.once("error", () => finish({ started: false, forbiddenReadSucceeded: true }));
          child.once("close", () => {
            const result = JSON.parse(fs.readFileSync(grandchildOutput, "utf8"));
            finish({ started: true, forbiddenReadSucceeded: result.forbiddenReadSucceeded });
          });
          await new Promise(() => {});
        }

        const [projectRoot, forbiddenFile, forbiddenWrite, junctionPath, scratchRoot, outputPath, nodePath] = process.argv.slice(3);
        const allowedEnvironment = new Set(["APPDATA", "COMSPEC", "LOCALAPPDATA", "NODE_OPTIONS", "PATH", "SYSTEMROOT", "TEMP", "TMP", "USERPROFILE"]);
        const result = {
          allowedReadSucceeded: canRead(`${projectRoot}\\package.json`),
          allowedWriteSucceeded: canWrite(`${scratchRoot}\\node-written.txt`),
          forbiddenReadSucceeded: canRead(forbiddenFile),
          forbiddenWriteSucceeded: canWrite(forbiddenWrite),
          junctionReadSucceeded: canRead(`${junctionPath}\\forbidden.txt`),
          unexpectedEnvironmentNames: Object.keys(process.env).filter((name) => !allowedEnvironment.has(name.toUpperCase())).sort()
        };
        fs.writeFileSync(outputPath, JSON.stringify(result, null, 2));
        """;
}
