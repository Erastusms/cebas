using System.Diagnostics;

namespace CEBAS.Infrastructure.Observability;

/// <summary>
/// Central OpenTelemetry ActivitySource for CEBAS distributed tracing.
/// </summary>
public static class CebasActivitySource
{
    public const string SourceName = "CEBAS";
    public static readonly ActivitySource Instance = new(SourceName, "1.0.0");
}
