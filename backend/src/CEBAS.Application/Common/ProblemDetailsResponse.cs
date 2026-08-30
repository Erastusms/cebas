namespace CEBAS.Application.Common;

/// <summary>
/// RFC 7807 Problem Details serialization contract.
/// </summary>
public class ProblemDetailsResponse
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? TraceId { get; set; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
}
