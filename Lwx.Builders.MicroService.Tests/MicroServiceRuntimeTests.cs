using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Lwx.Builders.MicroService.Tests;

[CollectionDefinition("MicroServiceTests", DisableParallelization = true)]
public class MicroServiceTestsCollection { }

[Collection("MicroServiceTests")]
public class MicroServiceRuntimeTests
{
    private static ProcessStartInfo BuildStartInfo(string projectName, string? env, string url)
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var projectPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.MicroService.Tests", "Projects", projectName);
        var projFile = System.IO.Path.Combine(projectPath, projectName + ".csproj");

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{projFile}\"")
        {
            WorkingDirectory = projectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(env))
        {
            psi.Environment["DOTNET_ENVIRONMENT"] = env;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = env;
        }

        psi.Environment["ASPNETCORE_URLS"] = url;

        return psi;
    }

    private static async Task<(int exitCode, string output, string response, int noneCode, string noneBody, int devCode, string devBody, int prdCode, string prdBody)> RunAndExercise(string projectName, string env, string url)
    {
        // Start the sample using a shell which redirects stdout/stderr into a temp file.
        var tmpLog = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"microservice-test-{Guid.NewGuid():N}.log");

        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var projectPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.MicroService.Tests", "Projects", projectName);
        var projFile = System.IO.Path.Combine(projectPath, projectName + ".csproj");

        // Build a safe shell command that sets environment variables and redirects output into the tmp file.
        // We'll set the important env vars on the ProcessStartInfo.Environment dictionary
        // which ensures they are present for the child dotnet process regardless of shell quoting.
        // Use >> so logs are appended if rerun while tmpLog exists; ensure the tmp log is empty for this run
        if (System.IO.File.Exists(tmpLog))
        {
            try { System.IO.File.Delete(tmpLog); } catch { }
        }
        // Build the project for release and then run the compiled DLL. Running the compiled assembly
        // avoids potential 'dotnet run' wrapper differences and is more deterministic under the test runner.
        var build = Process.Start(new ProcessStartInfo("dotnet", $"build \"{projFile}\" -c Release") { WorkingDirectory = projectPath, UseShellExecute = false, CreateNoWindow = true });
        if (build == null) throw new InvalidOperationException("failed to start build");
        build.WaitForExit();
        if (build.ExitCode != 0) throw new InvalidOperationException("build failed for test project");

        var dllPath = System.IO.Path.Combine(projectPath, "bin", "Release", "net9.0", projectName + ".dll");

        // Build inline env prefix so the executed process inherits the intended environment in the shell
        var cmdEnvPrefix = string.Empty;
        if (!string.IsNullOrEmpty(env)) cmdEnvPrefix = $"DOTNET_ENVIRONMENT={env} ASPNETCORE_ENVIRONMENT={env} ";

        // run the compiled DLL and append stdout/stderr to the temp log (no extra markers — avoid adapter/UI interference)
        var cmd = $"DOTNET_USE_POLLING_FILE_WATCHER=1 {cmdEnvPrefix} dotnet \"{dllPath}\" >> \"{tmpLog}\" 2>&1";

        var psi = new ProcessStartInfo("bash", $"-lc \"{cmd}\"")
        {
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ensure child env variables are set (use polling watcher to avoid inotify limit)
        System.Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        psi.Environment["DOTNET_USE_POLLING_FILE_WATCHER"] = "1";
        psi.Environment["ASPNETCORE_URLS"] = url;
        if (!string.IsNullOrEmpty(env))
        {
            psi.Environment["DOTNET_ENVIRONMENT"] = env;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = env;
        }

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("failed to start dotnet run via shell");

        var client = new System.Net.Http.HttpClient() { Timeout = TimeSpan.FromSeconds(2) };

        // wait for server to be up
        // Wait for either the output markers (Now listening) or health endpoint to become available
        var started = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (p.HasExited)
            {
                // ensure we stopped reading - read file for diagnostics
                var content = System.IO.File.Exists(tmpLog) ? System.IO.File.ReadAllText(tmpLog) : string.Empty;
                if (content.IndexOf("inotify", StringComparison.OrdinalIgnoreCase) >= 0
                    || content.IndexOf("user limit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException("Server could not start due to host inotify/file-watcher limits. Try increasing fs.inotify.max_user_instances (e.g. 'sudo sysctl -w fs.inotify.max_user_instances=1024') or run tests from a shell with fewer watchers.\nOutput:\n" + content);
                }

                throw new InvalidOperationException("Server process exited before reporting health. Output:\n" + content);
            }

            // prefer file detection which is safer for the test runner than attaching to stdout
            try
            {
                if (System.IO.File.Exists(tmpLog))
                {
                    var text = System.IO.File.ReadAllText(tmpLog);
                    if (text.Contains("Now listening on") || text.Contains("LwxEndpoints"))
                    {
                        started = true;
                        break;
                    }
                }
            }
            catch { /* ignore transient IO race */ }

            try
            {
                var r = await client.GetAsync(url + "/healthz");
                if (r.IsSuccessStatusCode)
                {
                    started = true;
                    break;
                }
            }
            catch { }

            await Task.Delay(100);
        }
        if (!started)
        {
            try { p.Kill(true); } catch { }
            var content = System.IO.File.Exists(tmpLog) ? System.IO.File.ReadAllText(tmpLog) : string.Empty;
            if (content.IndexOf("inotify", StringComparison.OrdinalIgnoreCase) >= 0
                || content.IndexOf("user limit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("Server process failed to start due to host inotify/file-watcher limits. Increase fs.inotify.max_user_instances or reduce watchers in your environment.\nOutput:\n" + content);
            }

            throw new InvalidOperationException("Server did not start. Output:\n" + content);
        }

        var health = await client.GetStringAsync(url + "/healthz");

        async Task<(int code, string body)> TryGet(string path)
        {
            try
            {
                var r = await client.GetAsync(url + path);
                var b = await r.Content.ReadAsStringAsync();
                return ((int)r.StatusCode, b ?? string.Empty);
            }
            catch { return (0, string.Empty); }
        }

        var n0 = await TryGet("/hello-none");
        var d0 = await TryGet("/hello-dev");
        var p0 = await TryGet("/hello-prd");

        // wait for workers to run
        await Task.Delay(1200);

        var rstatus = await client.GetAsync(url + "/worker-status");
        var js = await rstatus.Content.ReadAsStringAsync();

        // wait up to 10s for the process to exit; poll HasExited to avoid adapter races
        var waitSw = System.Diagnostics.Stopwatch.StartNew();
        while (!p.HasExited && waitSw.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(100);
        }
        if (!p.HasExited)
        {
            try { p.Kill(true); } catch { }
        }

        // give short grace for the child to flush output into the logfile, then read it.
        try { await System.Threading.Tasks.Task.Delay(200).ConfigureAwait(false); } catch { }
        string outStr;
        try { outStr = System.IO.File.Exists(tmpLog) ? System.IO.File.ReadAllText(tmpLog) : string.Empty; } catch { outStr = string.Empty; }
        try { System.IO.File.Delete(tmpLog); } catch { }

        return (p.HasExited ? p.ExitCode : -1, outStr, js, n0.code, n0.body, d0.code, d0.body, p0.code, p0.body);
    }

    [Fact]
    public async Task Scenario_Development_ShouldRunDevWorker_AndReturnDevEndpoint()
    {
        var (exit, output, json, nCode, nBody, dCode, dBody, pCode, pBody) = await RunAndExercise("MicroService", "Development", "http://127.0.0.1:5010");

        Assert.Equal(0, exit);
        Assert.Contains("LwxWorkerDescriptors", output);

        // dump server output + returned json to help debug flaky test behavior
        Console.WriteLine("MicroService output:\n" + output);
        Console.WriteLine("/worker-status json:\n" + json);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var none = root.GetProperty("none").GetInt32();
        var dev = root.GetProperty("dev").GetInt32();
        var prd = root.GetProperty("prd").GetInt32();

        // Endpoint behavior in Development: /hello-none -> 404, /hello-dev -> 200, /hello-prd -> 200
        Assert.Equal(404, nCode);
        Assert.Equal(200, dCode);
        Assert.Equal("hello-dev", dBody);
        Assert.Equal(200, pCode);
        Assert.Equal("hello-prd", pBody);

        Assert.True(none == 0, "none was " + none + "\nMicroService output:\n" + output + "\n/worker-status json:\n" + json);
        Assert.True(dev > 0, "dev worker should have incremented counter");
        // Production-stage workers are also registered in Development by generator semantics
        Assert.True(prd > 0, "prod worker should also be active in Development (generator includes Production-stage items in both environments)");
    }

    [Fact]
    public async Task Scenario_Production_ShouldRunPrdWorker_AndReturnPrdEndpoint()
    {
        var (exit, output, json, nCode, nBody, dCode, dBody, pCode, pBody) = await RunAndExercise("MicroService", "Production", "http://127.0.0.1:5011");

        Assert.Equal(0, exit);
        Assert.Contains("LwxWorkerDescriptors", output);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var none = root.GetProperty("none").GetInt32();
        var dev = root.GetProperty("dev").GetInt32();
        var prd = root.GetProperty("prd").GetInt32();

        // Endpoint behavior in Production: /hello-none -> 404, /hello-dev -> 404, /hello-prd -> 200
        Assert.Equal(404, nCode);
        Assert.Equal(404, dCode);
        Assert.Equal(200, pCode);
        Assert.Equal("hello-prd", pBody);

        Assert.True(none == 0, "none was " + none + "\nMicroService output:\n" + output + "\n/worker-status json:\n" + json);
        Assert.Equal(0, dev);
        Assert.True(prd > 0, "prd worker should have incremented counter");
    }
}
