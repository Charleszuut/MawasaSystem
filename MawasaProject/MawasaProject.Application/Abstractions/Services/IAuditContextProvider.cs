namespace MawasaProject.Application.Abstractions.Services;

public sealed record AuditContextSnapshot(
    string DeviceIpAddress,
    string DeviceName,
    string OsDescription,
    string AppVersion);

public interface IAuditContextProvider
{
    AuditContextSnapshot Capture();
}
