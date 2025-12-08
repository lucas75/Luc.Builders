using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxTimerTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
)
{
    public void Execute()
    {
        // Enforce file path and namespace matching
        ProcessorUtils.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);

        var name = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        // Extract attribute properties
        var (cronExpression, intervalSeconds, stageLiteral, runOnStartup, summary, description, namingException) 
            = ExtractAttributeMetadata();

        // Validate naming convention - should start with EndpointTimer
        if (!ValidateTimerNaming(name, namingException))
        {
            return;
        }

        // Validate namespace is under Endpoints
        if (!ValidateNamespace(ns))
        {
            return;
        }

        // Analyze the Execute method parameters for DI
        var (executeParams, hasCancellationToken, returnsTask) = AnalyzeExecuteMethod();

        // Generate the hosted service and Configure method
        GenerateSourceFiles(name, ns, cronExpression, intervalSeconds, stageLiteral, runOnStartup, summary, description, namingException, executeParams, hasCancellationToken, returnsTask);

        // Register with service - use interval or cron for schedule display
        var servicePrefix = Generator.ComputeServicePrefix(ns);
        var reg = parent.GetOrCreateRegistration(servicePrefix);
        reg.TimerNames.Add(ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation));
        var scheduleDisplay = intervalSeconds > 0 ? $"every {intervalSeconds}s" : (cronExpression ?? "0 0 * * * *");
        reg.TimerInfos.Add((
            ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation),
            scheduleDisplay
        ));
    }

    private (string? cronExpression, int intervalSeconds, string stageLiteral, bool runOnStartup, string? summary, string? description, string? namingException) ExtractAttributeMetadata()
    {
        string? cronExpression = null;
        int intervalSeconds = 0;
        string stageLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.All";
        bool runOnStartup = false;
        string? summary = null;
        string? description = null;
        string? namingException = null;

        if (attr.AttributeData == null) 
            return (cronExpression, intervalSeconds, stageLiteral, runOnStartup, summary, description, namingException);

        var named = attr.AttributeData.ToNamedArgumentMap();

        // CronExpression (also check constructor arg)
        if (named.TryGetValue("CronExpression", out var cronTc) && cronTc.Value is string s && !string.IsNullOrEmpty(s))
        {
            cronExpression = s;
        }
        else if (attr.AttributeData.ConstructorArguments.Length > 0 && attr.AttributeData.ConstructorArguments[0].Value is string cs && !string.IsNullOrEmpty(cs))
        {
            cronExpression = cs;
        }

        // IntervalSeconds
        if (named.TryGetValue("IntervalSeconds", out var ivTc) && ivTc.Value is int ivVal)
        {
            intervalSeconds = ivVal;
        }

        // Stage
        if (named.TryGetValue("Stage", out var stageTc) && stageTc.Value != null)
        {
            stageLiteral = ParseStageLiteral(stageTc.Value);
        }

        // RunOnStartup
        if (named.TryGetValue("RunOnStartup", out var runTc) && runTc.Value is bool runVal)
        {
            runOnStartup = runVal;
        }

        // Summary
        if (named.TryGetValue("Summary", out var sumTc) && sumTc.Value is string sumVal)
        {
            summary = sumVal;
        }

        // Description
        if (named.TryGetValue("Description", out var descTc) && descTc.Value is string descVal)
        {
            description = descVal;
        }

        // NamingExceptionJustification
        if (named.TryGetValue("NamingExceptionJustification", out var neTc) && neTc.Value is string neVal)
        {
            namingException = neVal?.Trim();
        }

        return (cronExpression, intervalSeconds, stageLiteral, runOnStartup, summary, description, namingException);
    }

    private static string ParseStageLiteral(object raw)
    {
        if (raw is int iv)
        {
            return iv switch
            {
                1 => "Lwx.Builders.MicroService.Atributtes.LwxStage.DevelopmentOnly",
                2 => "Lwx.Builders.MicroService.Atributtes.LwxStage.All",
                _ => "Lwx.Builders.MicroService.Atributtes.LwxStage.None"
            };
        }
        else
        {
            var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributtes.LwxStage.All";
            return tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributtes.LwxStage." + tmp);
        }
    }

    private bool ValidateTimerNaming(string name, string? namingException)
    {
        // Naming convention: EndpointTimer{Name}
        if (!name.StartsWith("EndpointTimer", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(namingException))
            {
                return true; // Exception provided
            }

            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX060",
                    "Invalid timer endpoint class name",
                    "Timer endpoint class '{0}' must start with 'EndpointTimer'. Example: EndpointTimerCleanup",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, attr.TargetSymbol.Name));
            return false;
        }

        return true;
    }

    private bool ValidateNamespace(string ns)
    {
        // Timers should be in .Endpoints namespace
        if (!ns.Contains(".Endpoints", StringComparison.Ordinal))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX061",
                    "Invalid timer endpoint namespace",
                    "Timer endpoint class must be in a namespace containing '.Endpoints'. Found: '{0}'",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, ns));
            return false;
        }

        return true;
    }

    private (List<(string ParamName, string ParamType)> DiParams, bool HasCancellationToken, bool ReturnsTask) AnalyzeExecuteMethod()
    {
        var result = new List<(string ParamName, string ParamType)>();
        var hasCancellationToken = false;
        var returnsTask = false;
        
        if (attr.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return (result, hasCancellationToken, returnsTask);

        var executeMethods = typeSymbol.GetMembers("Execute")
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic)
            .ToList();

        if (executeMethods.Count == 0)
            return (result, hasCancellationToken, returnsTask);

        var executeMethod = executeMethods[0];
        
        // Check if the method returns Task or Task<T>
        var returnType = executeMethod.ReturnType.ToDisplayString();
        returnsTask = returnType.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) 
                   || returnType == "Task" 
                   || returnType.StartsWith("Task<", StringComparison.Ordinal);
        
        foreach (var param in executeMethod.Parameters)
        {
            // Check for CancellationToken - we provide that
            if (param.Type.ToDisplayString() == "System.Threading.CancellationToken")
            {
                hasCancellationToken = true;
                continue;
            }
            result.Add((param.Name, param.Type.ToDisplayString()));
        }

        return (result, hasCancellationToken, returnsTask);
    }

    private void GenerateSourceFiles(
        string name, string ns, string? cronExpression, int intervalSeconds, string stageLiteral,
        bool runOnStartup, string? summary, string? description, string? namingException,
        List<(string ParamName, string ParamType)> executeParams, bool hasCancellationToken, bool returnsTask)
    {
        var shortStage = stageLiteral.Contains('.')
            ? string.Join('.', stageLiteral.Split('.').Skip(Math.Max(0, stageLiteral.Split('.').Length - 2)))
            : stageLiteral;

        // Build DI parameter list for the hosted service (excluding CancellationToken)
        var diParamsCallArgs = string.Join(", ", executeParams.Select(p => p.ParamName));
        
        // Build the full execute call arguments
        var executeCallArgs = hasCancellationToken
            ? (string.IsNullOrEmpty(diParamsCallArgs) ? "ct" : $"ct, {diParamsCallArgs}")
            : diParamsCallArgs;
        
        // Build the Execute call statement with proper await handling
        var executeCallStmt = returnsTask
            ? $"await {name}.Execute({executeCallArgs});"
            : $"{name}.Execute({executeCallArgs}); await Task.CompletedTask;";

        // Check if stage is active
        var stageActive = !stageLiteral.EndsWith(".None", StringComparison.Ordinal);

        string configureBuilderMethod;
        string configureAppMethod;

        if (!stageActive)
        {
            configureBuilderMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
    // Stage={{shortStage}} - Timer disabled
}
""";
            configureAppMethod = $$"""
public static void Configure(WebApplication app)
{
    // Stage={{shortStage}} - Timer disabled
}
""";
        }
        else
        {
            var stageCondExpr = stageLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                ? "builder.Environment.IsDevelopment()"
                : "builder.Environment.IsDevelopment() || builder.Environment.IsProduction()";

            configureBuilderMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
    // Stage={{shortStage}}
    if ({{stageCondExpr}})
    {
        // Register descriptor for health/monitoring
        builder.Services.AddSingleton(new LwxTimerDescriptor
        {
            Name = "{{GeneratorUtils.EscapeForCSharp(name)}}",
            Description = "{{GeneratorUtils.EscapeForCSharp(description ?? string.Empty)}}",
            CronExpression = "{{GeneratorUtils.EscapeForCSharp(cronExpression ?? "0 0 * * * *")}}",
            RunOnStartup = {{(runOnStartup ? "true" : "false")}}
        });

        // Register the hosted service that manages the timer
        builder.Services.AddHostedService<{{name}}HostedService>();
    }
}
""";

            configureAppMethod = $$"""
public static void Configure(WebApplication app)
{
    // Stage={{shortStage}} - No app configuration needed for timers
}
""";
        }

        // Generate the hosted service class
        var hostedServiceClass = string.Empty;
        if (stageActive)
        {
            if (intervalSeconds > 0)
            {
                // Interval-based timer
                hostedServiceClass = $$"""

/// <summary>
/// Hosted service that manages the interval-based timer endpoint lifecycle.
/// </summary>
public sealed class {{name}}HostedService : BackgroundService
{
    private readonly ILogger<{{name}}HostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly LwxTimerDescriptor _descriptor;
    private readonly int _intervalSeconds = {{intervalSeconds}};

    public {{name}}HostedService(ILogger<{{name}}HostedService> logger, IServiceProvider serviceProvider, LwxTimerDescriptor descriptor)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _descriptor = descriptor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting timer {Name} with interval {Interval}s",
            _descriptor.Name,
            _intervalSeconds);

        // Run on startup if configured
        if (_descriptor.RunOnStartup)
        {
            await ExecuteTimerAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _descriptor.NextExecutionTime = DateTimeOffset.UtcNow.AddSeconds(_intervalSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteTimerAsync(stoppingToken);
            }
        }
    }

    private async Task ExecuteTimerAsync(CancellationToken ct)
    {
        try
        {
            _descriptor.LastExecutionTime = DateTimeOffset.UtcNow;
            using var scope = _serviceProvider.CreateScope();
{{GenerateDiResolution(executeParams).FixIndent(3)}}
            {{executeCallStmt}}
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing timer {Name}", _descriptor.Name);
        }
    }
}
""";
            }
            else
            {
                // Cron-based timer
                hostedServiceClass = $$"""

/// <summary>
/// Hosted service that manages the cron-based timer endpoint lifecycle.
/// </summary>
public sealed class {{name}}HostedService : BackgroundService
{
    private readonly ILogger<{{name}}HostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly LwxTimerDescriptor _descriptor;

    public {{name}}HostedService(ILogger<{{name}}HostedService> logger, IServiceProvider serviceProvider, LwxTimerDescriptor descriptor)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _descriptor = descriptor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpressionStr = "{{GeneratorUtils.EscapeForCSharp(cronExpression ?? "0 0 * * * *")}}";
        var schedule = ParseCronExpression(cronExpressionStr);

        _logger.LogInformation(
            "Starting timer {Name} with schedule {Schedule}",
            _descriptor.Name,
            cronExpressionStr);

        // Run on startup if configured
        if (_descriptor.RunOnStartup)
        {
            await ExecuteTimerAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = GetNextOccurrence(schedule, DateTimeOffset.UtcNow);
            _descriptor.NextExecutionTime = nextRun;

            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteTimerAsync(stoppingToken);
            }
        }
    }

    private async Task ExecuteTimerAsync(CancellationToken ct)
    {
        try
        {
            _descriptor.LastExecutionTime = DateTimeOffset.UtcNow;
            using var scope = _serviceProvider.CreateScope();
{{GenerateDiResolution(executeParams).FixIndent(3)}}
            {{executeCallStmt}}
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing timer {Name}", _descriptor.Name);
        }
    }

    // Simple cron parser for 6-field format (second minute hour day month weekday)
    private static CronSchedule ParseCronExpression(string expr)
    {
        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CronSchedule
        {
            Second = parts.Length > 0 ? parts[0] : "0",
            Minute = parts.Length > 1 ? parts[1] : "*",
            Hour = parts.Length > 2 ? parts[2] : "*",
            Day = parts.Length > 3 ? parts[3] : "*",
            Month = parts.Length > 4 ? parts[4] : "*",
            WeekDay = parts.Length > 5 ? parts[5] : "*"
        };
    }

    private static DateTimeOffset GetNextOccurrence(CronSchedule schedule, DateTimeOffset from)
    {
        // Simple implementation - find next matching minute
        var next = from.AddSeconds(60 - from.Second);
        
        for (int i = 0; i < 60 * 24 * 7; i++) // Max 1 week ahead
        {
            if (MatchesSchedule(schedule, next))
            {
                return next;
            }
            next = next.AddMinutes(1);
        }
        
        return from.AddHours(1); // Fallback
    }

    private static bool MatchesSchedule(CronSchedule schedule, DateTimeOffset dt)
    {
        return MatchesField(schedule.Second, dt.Second) &&
               MatchesField(schedule.Minute, dt.Minute) &&
               MatchesField(schedule.Hour, dt.Hour) &&
               MatchesField(schedule.Day, dt.Day) &&
               MatchesField(schedule.Month, dt.Month) &&
               MatchesWeekDay(schedule.WeekDay, dt.DayOfWeek);
    }

    private static bool MatchesField(string field, int value)
    {
        if (field == "*") return true;
        
        // Handle */n syntax
        if (field.StartsWith("*/"))
        {
            if (int.TryParse(field[2..], out var step))
            {
                return value % step == 0;
            }
        }
        
        // Handle comma-separated values
        if (field.Contains(','))
        {
            return field.Split(',').Any(p => int.TryParse(p.Trim(), out var v) && v == value);
        }
        
        // Handle range
        if (field.Contains('-'))
        {
            var rangeParts = field.Split('-');
            if (rangeParts.Length == 2 && 
                int.TryParse(rangeParts[0], out var min) && 
                int.TryParse(rangeParts[1], out var max))
            {
                return value >= min && value <= max;
            }
        }
        
        // Handle single value
        if (int.TryParse(field, out var exact))
        {
            return exact == value;
        }
        
        return false;
    }

    private static bool MatchesWeekDay(string field, DayOfWeek dayOfWeek)
    {
        if (field == "*") return true;
        var value = (int)dayOfWeek;
        return MatchesField(field, value);
    }

    private class CronSchedule
    {
        public string Second { get; set; } = "0";
        public string Minute { get; set; } = "*";
        public string Hour { get; set; } = "*";
        public string Day { get; set; } = "*";
        public string Month { get; set; } = "*";
        public string WeekDay { get; set; } = "*";
    }
}
""";
            }
        }

        var source = $$"""
// <auto-generated/>
#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace {{ns}};

public partial class {{name}}
{
    {{configureBuilderMethod.FixIndent(1, indentFirstLine: false)}}

    {{configureAppMethod.FixIndent(1, indentFirstLine: false)}}
}
{{hostedServiceClass}}
""";

        var generatedFileName = !string.IsNullOrEmpty(namingException)
            ? $"{ns}.{name}.g.cs"
            : $"LwxTimer_{name}.g.cs";

        ProcessorUtils.AddGeneratedFile(ctx, generatedFileName, source);
    }

    private static string GenerateDiResolution(List<(string ParamName, string ParamType)> diParams)
    {
        if (diParams.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var p in diParams)
        {
            sb.AppendLine($"var {p.ParamName} = scope.ServiceProvider.GetRequiredService<{p.ParamType}>();");
        }
        return sb.ToString().TrimEnd();
    }
}
