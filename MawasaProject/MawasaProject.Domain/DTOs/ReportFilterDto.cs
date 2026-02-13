namespace MawasaProject.Domain.DTOs;

public sealed class ReportFilterDto
{
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
    public Guid? CustomerId { get; init; }
    public bool IncludeOverdueOnly { get; init; }
}
