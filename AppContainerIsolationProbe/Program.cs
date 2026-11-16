using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace AppContainerIsolationProbe;

internal static partial class Program
{
    private const int ProbeTimeoutMilliseconds = 20_000;
    private static readonly string[] AllowedEnvironmentNames =
    [
        "APPDATA", "COMSPEC", "LOCALAPPDATA", "NODE_OPTIONS", "PATH", "SYSTEMROOT", "TEMP", "TMP", "USERPROFILE",
    ];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "--child" => RunChild(args),
                "--grandchild" => RunGrandchild(args),
                "--hang" => RunHangingChild(args),
                "--hang-leaf" => RunHangingLeaf(),
                "--node-controller" => RunNodeController(args),
                _ => RunController(args),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static int RunController(string[] args)
    {
        string runRoot = args.Length == 1
            ? Path.GetFullPath(args[0])
            : Path.Combine(Path.GetTempPath(), $"appcontainer-probe-{Guid.NewGuid():N}");
        string fixtureRoot = Path.Combine(runRoot, "fixture");
        string allowedRoot = Path.Combine(fixtureRoot, "allowed");
        string forbiddenRoot = Path.Combine(fixtureRoot, "forbidden");
        string evidenceRoot = Path.Combine(runRoot, "evidence");
        string resultPath = Path.Combine(allowedRoot, "result.json");
        string allowedSentinel = Path.Combine(allowedRoot, "allowed.txt");
        string forbiddenSentinel = Path.Combine(forbiddenRoot, "forbidden.txt");
        string linkPath = Path.Combine(allowedRoot, "external-link");
        string profileName = $"AgenticOrchestrationBenchmark.P11.{Guid.NewGuid():N}";
        IntPtr appContainerSid = IntPtr.Zero;
        bool profileCreated = false;

        try
        {
            Directory.CreateDirectory(allowedRoot);
            Directory.CreateDirectory(forbiddenRoot);
            Directory.CreateDirectory(evidenceRoot);
            File.WriteAllText(allowedSentinel, "ALLOWED_SENTINEL", Encoding.UTF8);
            File.WriteAllText(forbiddenSentinel, "FORBIDDEN_SENTINEL", Encoding.UTF8);
            CreateJunction(linkPath, forbiddenRoot);

            int createResult = NativeMethods.CreateAppContainerProfile(
                profileName,
                profileName,
                "Temporary P11 isolation probe",
                IntPtr.Zero,
                0,
                out appContainerSid);
            if (createResult != 0)
            {
                Marshal.ThrowExceptionForHR(createResult);
            }
            profileCreated = true;

            string sidValue = ConvertSidToString(appContainerSid);
            var sid = new SecurityIdentifier(sidValue);
            GrantDirectoryAccess(allowedRoot, sid, FileSystemRights.Modify | FileSystemRights.ReadAndExecute);
            GrantDirectoryAccess(AppContext.BaseDirectory, sid, FileSystemRights.ReadAndExecute);

            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            using var httpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            httpListener.Start();
            int tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            int httpPort = ((IPEndPoint)httpListener.LocalEndpoint).Port;
            Task httpServer = ServeSingleHttpRequestAsync(httpListener);

            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Percorso dell'eseguibile non disponibile.");
            string[] childArguments =
            [
                "--child", allowedRoot, forbiddenSentinel, linkPath,
                Path.Combine(evidenceRoot, "forbidden-write.txt"),
                tcpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                httpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ];
            ProcessRunResult childRun = RunInAppContainer(
                executablePath,
                childArguments,
                AppContext.BaseDirectory,
                appContainerSid,
                ProbeTimeoutMilliseconds);
            tcpListener.Stop();
            httpListener.Stop();
            httpServer.Wait(TimeSpan.FromSeconds(1));
            if (childRun.TimedOut || childRun.ExitCode != 0 || !File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    $"Probe figlio non completato: timeout={childRun.TimedOut}, exitCode={childRun.ExitCode}.");
            }

            ChildProbeResult childResult = JsonSerializer.Deserialize<ChildProbeResult>(File.ReadAllText(resultPath), JsonOptions)
                ?? throw new InvalidOperationException("Risultato figlio non valido.");

            ProcessRunResult timeoutRun = RunInAppContainer(
                executablePath,
                ["--hang"],
                AppContext.BaseDirectory,
                appContainerSid,
                1_000);

            var report = new ControllerReport(
                profileName,
                sidValue,
                childRun,
                childResult,
                timeoutRun,
                Checks: new Dictionary<string, bool>
                {
                    ["allowedReadSucceeded"] = childResult.AllowedReadSucceeded,
                    ["allowedWriteSucceeded"] = childResult.AllowedWriteSucceeded,
                    ["forbiddenReadDenied"] = !childResult.ForbiddenReadSucceeded,
                    ["forbiddenWriteDenied"] = !childResult.ForbiddenWriteSucceeded,
                    ["junctionReadDenied"] = !childResult.JunctionReadSucceeded,
                    ["childProcessStarted"] = childResult.GrandchildStarted,
                    ["childProcessForbiddenReadDenied"] = !childResult.GrandchildForbiddenReadSucceeded,
                    ["dnsDenied"] = !childResult.DnsSucceeded,
                    ["tcpDenied"] = !childResult.TcpSucceeded,
                    ["httpDenied"] = !childResult.HttpSucceeded,
                    ["environmentAllowlisted"] = childResult.UnexpectedEnvironmentNames.Count == 0,
                    ["timeoutObserved"] = timeoutRun.TimedOut,
                    ["timeoutJobTerminated"] = timeoutRun.JobTerminated,
                });
            string evidencePath = Path.Combine(evidenceRoot, "p11-results.json");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
            Console.WriteLine($"RUN_ROOT={runRoot}");
            Console.WriteLine($"EVIDENCE_PATH={evidencePath}");

            return report.Checks.Values.All(value => value) ? 0 : 1;
        }
        finally
        {
            if (appContainerSid != IntPtr.Zero)
            {
                NativeMethods.LocalFree(appContainerSid);
            }

            if (profileCreated)
            {
                int deleteResult = NativeMethods.DeleteAppContainerProfile(profileName);
                if (deleteResult != 0)
                {
                    Console.Error.WriteLine($"Rimozione profilo fallita: HRESULT 0x{deleteResult:X8}.");
                }
            }

            TryDeleteJunction(linkPath);
            TryDeleteFixture(fixtureRoot);
        }
    }

    private static int RunChild(string[] args)
    {
        if (args.Length != 7)
        {
            throw new ArgumentException("Argomenti child non validi.");
        }

        string allowedRoot = args[1];
        string forbiddenFile = args[2];
        string linkPath = args[3];
        string forbiddenWritePath = args[4];
        string resultPath = Path.Combine(allowedRoot, "result.json");
        string allowedOutputPath = Path.Combine(allowedRoot, "written.txt");
        string grandchildResultPath = Path.Combine(allowedRoot, "grandchild.json");

        bool allowedRead = CanRead(Path.Combine(allowedRoot, "allowed.txt"));
        bool allowedWrite = CanWrite(allowedOutputPath);
        bool forbiddenRead = CanRead(forbiddenFile);
        bool forbiddenWrite = CanWrite(forbiddenWritePath);
        bool junctionRead = CanRead(Path.Combine(linkPath, "forbidden.txt"));

        using var grandchild = Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--grandchild", forbiddenFile, grandchildResultPath },
        });
        bool grandchildStarted = grandchild is not null;
        grandchild?.WaitForExit(5_000);
        GrandchildResult? grandchildResult = File.Exists(grandchildResultPath)
            ? JsonSerializer.Deserialize<GrandchildResult>(File.ReadAllText(grandchildResultPath), JsonOptions)
            : null;

        bool dnsSucceeded = TryDns();
        int tcpPort = int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
        int httpPort = int.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture);
        bool tcpSucceeded = TryTcp(tcpPort);
        bool httpSucceeded = TryHttp(httpPort);
        List<string> unexpectedNames = Environment.GetEnvironmentVariables().Keys
            .Cast<string>()
            .Where(name => !AllowedEnvironmentNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new ChildProbeResult(
            allowedRead,
            allowedWrite,
            forbiddenRead,
            forbiddenWrite,
            junctionRead,
            grandchildStarted,
            grandchildResult?.ForbiddenReadSucceeded ?? true,
            dnsSucceeded,
            tcpSucceeded,
            httpSucceeded,
            unexpectedNames);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8);
        return 0;
    }

    private static int RunGrandchild(string[] args)
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("Argomenti grandchild non validi.");
        }

        var result = new GrandchildResult(CanRead(args[1]));
        File.WriteAllText(args[2], JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8);
        return 0;
    }

    private static int RunHangingChild(string[] args)
    {
        _ = args;
        using Process? child = Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--hang-leaf" },
        });
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static int RunHangingLeaf()
    {
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static ProcessRunResult RunInAppContainer(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IntPtr appContainerSid,
        uint timeoutMilliseconds,
        IReadOnlyDictionary<string, string>? environmentOverrides = null,
        string? logPrefix = null)
    {
        IntPtr attributeList = IntPtr.Zero;
        IntPtr capabilitiesPointer = IntPtr.Zero;
        IntPtr environmentPointer = IntPtr.Zero;
        IntPtr job = IntPtr.Zero;
        FileStream? standardOutput = null;
        FileStream? standardError = null;
        Microsoft.Win32.SafeHandles.SafeFileHandle? standardInput = null;
        NativeMethods.ProcessInformation process = default;

        try
        {
            nuint attributeListSize = 0;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(checked((int)attributeListSize));
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
            {
                throw NativeMethods.Error("InitializeProcThreadAttributeList");
            }

            var capabilities = new NativeMethods.SecurityCapabilities { AppContainerSid = appContainerSid };
            capabilitiesPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.SecurityCapabilities>());
            Marshal.StructureToPtr(capabilities, capabilitiesPointer, false);
            if (!NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)NativeMethods.ProcThreadAttributeSecurityCapabilities,
                    capabilitiesPointer,
                    (nuint)Marshal.SizeOf<NativeMethods.SecurityCapabilities>(),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw NativeMethods.Error("UpdateProcThreadAttribute");
            }

            var startupInfo = new NativeMethods.StartupInfoEx
            {
                StartupInfo = new NativeMethods.StartupInfo { Cb = Marshal.SizeOf<NativeMethods.StartupInfoEx>() },
                AttributeList = attributeList,
            };
            bool inheritHandles = false;
            if (logPrefix is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPrefix)!);
                standardOutput = new FileStream(logPrefix + ".stdout.txt", FileMode.Create, FileAccess.Write, FileShare.Read);
                standardError = new FileStream(logPrefix + ".stderr.txt", FileMode.Create, FileAccess.Write, FileShare.Read);
                standardInput = File.OpenHandle("NUL", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                SetInheritable(standardOutput.SafeFileHandle.DangerousGetHandle());
                SetInheritable(standardError.SafeFileHandle.DangerousGetHandle());
                SetInheritable(standardInput.DangerousGetHandle());
                startupInfo.StartupInfo.Flags |= NativeMethods.StartfUseStdHandles;
                startupInfo.StartupInfo.StandardInput = standardInput.DangerousGetHandle();
                startupInfo.StartupInfo.StandardOutput = standardOutput.SafeFileHandle.DangerousGetHandle();
                startupInfo.StartupInfo.StandardError = standardError.SafeFileHandle.DangerousGetHandle();
                inheritHandles = true;
            }
            environmentPointer = CreateEnvironmentBlock(environmentOverrides);
            var commandLine = new StringBuilder(Quote(executablePath));
            foreach (string argument in arguments)
            {
                commandLine.Append(' ').Append(Quote(argument));
            }

            uint flags = NativeMethods.CreateNoWindow
                | NativeMethods.CreateSuspended
                | NativeMethods.CreateUnicodeEnvironment
                | NativeMethods.ExtendedStartupInfoPresent;
            if (!NativeMethods.CreateProcess(
                    executablePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles,
                    flags,
                    environmentPointer,
                    workingDirectory,
                    ref startupInfo,
                    out process))
            {
                throw NativeMethods.Error("CreateProcess");
            }

            job = CreateKillOnCloseJob();
            if (!NativeMethods.AssignProcessToJobObject(job, process.Process))
            {
                throw NativeMethods.Error("AssignProcessToJobObject");
            }
            if (NativeMethods.ResumeThread(process.Thread) == uint.MaxValue)
            {
                throw NativeMethods.Error("ResumeThread");
            }

            uint waitResult = NativeMethods.WaitForSingleObject(process.Process, timeoutMilliseconds);
            bool timedOut = waitResult == NativeMethods.WaitTimeout;
            bool jobTerminated = false;
            if (timedOut)
            {
                jobTerminated = NativeMethods.TerminateJobObject(job, 124);
                NativeMethods.WaitForSingleObject(process.Process, 5_000);
            }
            else if (waitResult != NativeMethods.WaitObject0)
            {
                throw NativeMethods.Error("WaitForSingleObject");
            }

            if (!NativeMethods.GetExitCodeProcess(process.Process, out uint exitCode))
            {
                throw NativeMethods.Error("GetExitCodeProcess");
            }
            return new ProcessRunResult(timedOut, jobTerminated, exitCode);
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) NativeMethods.CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) NativeMethods.CloseHandle(process.Process);
            if (job != IntPtr.Zero) NativeMethods.CloseHandle(job);
            standardInput?.Dispose();
            standardError?.Dispose();
            standardOutput?.Dispose();
            if (environmentPointer != IntPtr.Zero) Marshal.FreeHGlobal(environmentPointer);
            if (capabilitiesPointer != IntPtr.Zero) Marshal.FreeHGlobal(capabilitiesPointer);
            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }
    }

    private static void SetInheritable(IntPtr handle)
    {
        if (!NativeMethods.SetHandleInformation(handle, NativeMethods.HandleFlagInherit, NativeMethods.HandleFlagInherit))
        {
            throw NativeMethods.Error("SetHandleInformation");
        }
    }

    private static IntPtr CreateKillOnCloseJob()
    {
        IntPtr job = NativeMethods.CreateJobObject(IntPtr.Zero, IntPtr.Zero);
        if (job == IntPtr.Zero)
        {
            throw NativeMethods.Error("CreateJobObject");
        }

        var limits = new NativeMethods.JobObjectExtendedLimitInformation();
        limits.BasicLimitInformation.LimitFlags = NativeMethods.JobObjectLimitKillOnJobClose;
        int size = Marshal.SizeOf<NativeMethods.JobObjectExtendedLimitInformation>();
        IntPtr pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, pointer, false);
            if (!NativeMethods.SetInformationJobObject(
                    job,
                    NativeMethods.JobObjectExtendedLimitInformationClass,
                    pointer,
                    checked((uint)size)))
            {
                throw NativeMethods.Error("SetInformationJobObject");
            }
            return job;
        }
        catch
        {
            NativeMethods.CloseHandle(job);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static IntPtr CreateEnvironmentBlock(IReadOnlyDictionary<string, string>? overrides)
    {
        string content = string.Join(
            '\0',
            AllowedEnvironmentNames.Select(name =>
                $"{name}={(overrides is not null && overrides.TryGetValue(name, out string? value) ? value : Environment.GetEnvironmentVariable(name) ?? string.Empty)}")) + "\0\0";
        byte[] bytes = Encoding.Unicode.GetBytes(content);
        IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return pointer;
    }

    private static void GrantDirectoryAccess(string path, SecurityIdentifier sid, FileSystemRights rights)
    {
        var directory = new DirectoryInfo(path);
        DirectorySecurity security = directory.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        directory.SetAccessControl(security);
    }

    private static void CreateJunction(string linkPath, string targetPath)
    {
        string commandInterpreter = Environment.GetEnvironmentVariable("COMSPEC")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = commandInterpreter,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "/d", "/c", "mklink", "/J", linkPath, targetPath },
        });
        if (process is null)
        {
            throw new InvalidOperationException("Avvio di mklink non riuscito.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Creazione junction non riuscita: {process.StandardError.ReadToEnd()}");
        }
    }

    private static string ConvertSidToString(IntPtr sid)
    {
        if (!NativeMethods.ConvertSidToStringSid(sid, out IntPtr stringSid))
        {
            throw NativeMethods.Error("ConvertSidToStringSid");
        }
        try
        {
            return Marshal.PtrToStringUni(stringSid)
                ?? throw new InvalidOperationException("SID AppContainer non valido.");
        }
        finally
        {
            NativeMethods.LocalFree(stringSid);
        }
    }

    private static bool CanRead(string path)
    {
        try
        {
            _ = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool CanWrite(string path)
    {
        try
        {
            File.WriteAllText(path, "PROBE", Encoding.UTF8);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool TryDns()
    {
        try
        {
            _ = Dns.GetHostAddresses("example.com");
            return true;
        }
        catch (Exception exception) when (exception is SocketException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryTcp(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(2));
            return client.Connected;
        }
        catch (Exception exception) when (exception is SocketException or AggregateException)
        {
            return false;
        }
    }

    private static bool TryHttp(int port)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using HttpResponseMessage response = client.GetAsync($"http://127.0.0.1:{port}/probe").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static async Task ServeSingleHttpRequestAsync(TcpListener listener)
    {
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using NetworkStream stream = client.GetStream();
            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
            await stream.WriteAsync(response).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            // La chiusura del listener termina il controllo negativo senza lasciare task pendenti.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string Quote(string value) => '"' + value.Replace("\"", "\\\"") + '"';

    private static void TryDeleteFixture(string fixtureRoot)
    {
        try
        {
            if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Pulizia fixture fallita: {exception.Message}");
        }
    }

    private static void TryDeleteJunction(string linkPath)
    {
        try
        {
            if (!Directory.Exists(linkPath)) return;
            string commandInterpreter = Environment.GetEnvironmentVariable("COMSPEC")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = commandInterpreter,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "/d", "/c", "rmdir", linkPath },
            });
            process?.WaitForExit();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Pulizia junction fallita: {exception.Message}");
        }
    }

    private sealed record ProcessRunResult(bool TimedOut, bool JobTerminated, uint ExitCode);
    private sealed record GrandchildResult(bool ForbiddenReadSucceeded);
    private sealed record ChildProbeResult(
        bool AllowedReadSucceeded,
        bool AllowedWriteSucceeded,
        bool ForbiddenReadSucceeded,
        bool ForbiddenWriteSucceeded,
        bool JunctionReadSucceeded,
        bool GrandchildStarted,
        bool GrandchildForbiddenReadSucceeded,
        bool DnsSucceeded,
        bool TcpSucceeded,
        bool HttpSucceeded,
        List<string> UnexpectedEnvironmentNames);
    private sealed record ControllerReport(
        string ProfileName,
        string AppContainerSid,
        ProcessRunResult ChildRun,
        ChildProbeResult ChildResult,
        ProcessRunResult TimeoutRun,
        Dictionary<string, bool> Checks);
}
