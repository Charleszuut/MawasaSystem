using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using MawasaProject.Application.Abstractions.Services;

namespace MawasaProject.Infrastructure.Services.Audit;

public sealed class RuntimeAuditContextProvider : IAuditContextProvider
{
    private readonly string _deviceIpAddress;
    private readonly string _deviceName;
    private readonly string _osDescription;
    private readonly string _appVersion;

    public RuntimeAuditContextProvider()
    {
        _deviceIpAddress = ResolveIpAddress();
        _deviceName = Environment.MachineName;
        _osDescription = RuntimeInformation.OSDescription;
        _appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
    }

    public AuditContextSnapshot Capture()
    {
        return new AuditContextSnapshot(
            _deviceIpAddress,
            _deviceName,
            _osDescription,
            _appVersion);
    }

    private static string ResolveIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(address =>
                address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address));
            return ip?.ToString() ?? "offline-local";
        }
        catch
        {
            return "offline-local";
        }
    }
}
