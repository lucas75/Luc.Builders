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
    // BuildStartInfo removed — tests now use ExecuteAsync to build and run sample projects.


    /// <summary>
    /// Async version of Execute that builds and runs a project and returns stdout/stderr
    /// when the run process exits. Returns a tuple (stdout, stderr).
    /// </summary>
    private static async Task<(string stdout, string stderr)> ExecuteAsync(string projectDir, System.Collections.Generic.IDictionary<string, string>? environment = null, int timeoutMs = 120000)
    {
        if (string.IsNullOrWhiteSpace(projectDir)) throw new ArgumentException("projectDir must be provided", nameof(projectDir));

        // Resolve to project file
        var projectFile = projectDir;
        if (!projectFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            if (!System.IO.Directory.Exists(projectDir)) throw new System.IO.DirectoryNotFoundException(projectDir);
            var csprojs = System.IO.Directory.GetFiles(projectDir, "*.csproj", System.IO.SearchOption.TopDirectoryOnly);
            if (csprojs.Length != 1) throw new InvalidOperationException($"Expected exactly one .csproj in '{projectDir}' but found {csprojs.Length}");
            projectFile = csprojs[0];
        }

        // dotnet build -c Release
        var buildInfo = new ProcessStartInfo("dotnet", $"build \"{projectFile}\" -c Release")
        {
            WorkingDirectory = System.IO.Path.GetDirectoryName(projectFile) ?? projectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Apply only the provided environment variables to the build process (do NOT mutate global process env)
        if (environment != null)
        {
            foreach (var kv in environment)
            {
                try { buildInfo.Environment[kv.Key] = kv.Value ?? string.Empty; } catch { }
            }
        }

        using (var build = Process.Start(buildInfo) ?? throw new InvalidOperationException("failed to start dotnet build"))
        {
            var outTask = build.StandardOutput.ReadToEndAsync();
            var errTask = build.StandardError.ReadToEndAsync();

            var completed = await Task.WhenAny(Task.Run(() => build.WaitForExit()), Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (!build.HasExited) try { build.Kill(true); } catch { }

            var stdoutBuild = await outTask.ConfigureAwait(false);
            var stderrBuild = await errTask.ConfigureAwait(false);

            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet build failed for '{projectFile}'. ExitCode={build.ExitCode}\nstdout:\n{stdoutBuild}\nstderr:\n{stderrBuild}");
            }
        }

        // dotnet run --project
        var runInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectFile}\" -c Release")
        {
            WorkingDirectory = System.IO.Path.GetDirectoryName(projectFile) ?? projectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Apply only the provided environment variables to the run process (do NOT mutate global process env)
        if (environment != null)
        {
            foreach (var kv in environment)
            {
                try { runInfo.Environment[kv.Key] = kv.Value ?? string.Empty; } catch { }
            }
        }

        using var run = Process.Start(runInfo) ?? throw new InvalidOperationException("failed to start dotnet run");
        var outRead = run.StandardOutput.ReadToEndAsync();
        var errRead = run.StandardError.ReadToEndAsync();

        var exitTask = Task.Run(() => run.WaitForExit());
        var finished = await Task.WhenAny(exitTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
        if (!run.HasExited)
        {
            try { run.Kill(true); } catch { }
        }

        var stdout = await outRead.ConfigureAwait(false);
        var stderr = await errRead.ConfigureAwait(false);

        // Append a marker so callers can easily detect the run's exit status
        var exitCode = run.HasExited ? run.ExitCode : -1;
        stderr = (stderr ?? string.Empty) + (stderr?.EndsWith("\n") == true ? string.Empty : "\n") + $"RESULT: {exitCode}\n";

        return (stdout, stderr);
    }

    // RunAndExerciseSubprocessAsync removed — tests use ExecuteAsync for subprocess runs and assert on stdout/stderr logs.

    // RunAndExerciseInProcess removed — tests now use ExecuteAsync which runs sample projects as subprocesses.

    [Fact]
    public async Task Scenario_Development_ShouldRunDevWorker_AndReturnDevEndpoint()
    {
        // Prepare env and project path
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var projectPath = Path.Combine(repoRoot, "Lwx.Builders.MicroService.Tests", "Projects", "MicroService");

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:5010"
        };

        var (stdout, stderr) = await ExecuteAsync(projectPath, env);

        // parse exit code from stderr marker
        var exit = -1;
        if (!string.IsNullOrEmpty(stderr))
        {
            var idx = stderr.LastIndexOf("RESULT:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var s = stderr[(idx + "RESULT:".Length)..].Trim();
                if (int.TryParse(s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim(), out var v)) exit = v;
            }
        }

        Assert.Equal(0, exit);
        var combined = (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
        Assert.Contains("LwxWorkerDescriptors", combined);

        // Confirm basic HTTP activity happened (health endpoint + other endpoints tested by the service)
        Assert.Contains("Now listening on", combined);
        Assert.Contains("LwxEndpoints", combined);

        // Workers should log heartbeats
        Assert.Contains("WorkerDev heartbeat", combined);
        Assert.Contains("WorkerPrd heartbeat", combined);

        // Dump stdout/stderr for troubleshooting when running in CI locally
        Console.WriteLine("MicroService stdout:\n" + stdout);
        Console.WriteLine("MicroService stderr:\n" + stderr);
        Console.WriteLine("MicroService combined:\n" + combined);
        // no global env mutated; nothing to restore
    }

    [Fact]
    public async Task Scenario_Production_ShouldRunPrdWorker_AndReturnPrdEndpoint()
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var projectPath = System.IO.Path.Combine(repoRoot, "Lwx.Builders.MicroService.Tests", "Projects", "MicroService");

        var env = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:5011"
        };

        var (stdout, stderr) = await ExecuteAsync(projectPath, env);

        // parse exit code
        var exit = -1;
        if (!string.IsNullOrEmpty(stderr))
        {
            var idx = stderr.LastIndexOf("RESULT:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var s = stderr[(idx + "RESULT:".Length)..].Trim();
                if (int.TryParse(s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim(), out var v)) exit = v;
            }
        }

        Assert.Equal(0, exit);

        var combined = (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
        Assert.Contains("LwxWorkerDescriptors", combined);

        // Confirm basic HTTP activity happened
        Assert.Contains("Now listening on", combined);
        Assert.Contains("LwxEndpoints", combined);

        // Worker heartbeats should include production worker
        Assert.Contains("WorkerPrd heartbeat", combined);

        Console.WriteLine("MicroService stdout:\n" + stdout);
        Console.WriteLine("MicroService stderr:\n" + stderr);
        Console.WriteLine("MicroService combined:\n" + combined);
        // no global env mutated; nothing to restore
    }
}
