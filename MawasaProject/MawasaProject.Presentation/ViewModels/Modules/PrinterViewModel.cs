using System.Collections.ObjectModel;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.Entities;
using MawasaProject.Presentation.Services.Dialogs;
using MawasaProject.Presentation.ViewModels.Core;

namespace MawasaProject.Presentation.ViewModels.Modules;

public sealed class PrinterViewModel : BaseViewModel
{
    private readonly IPrinterService _printerService;
    private readonly IDialogService _dialogService;

    private string _selectedPrinter = string.Empty;
    private PrinterProfile? _selectedProfile;
    private string _profileName = string.Empty;
    private string _paperSize = "A4";
    private bool _isDefaultProfile = true;
    private bool _isActiveProfile = true;

    public PrinterViewModel()
        : this(
            App.Services.GetRequiredService<IPrinterService>(),
            App.Services.GetRequiredService<IDialogService>())
    {
    }

    public PrinterViewModel(IPrinterService printerService, IDialogService dialogService)
    {
        _printerService = printerService;
        _dialogService = dialogService;

        PaperSizes.Add("A4");
        PaperSizes.Add("A5");
        PaperSizes.Add("Letter");
        PaperSizes.Add("Thermal-80mm");
    }

    public ObservableCollection<string> Printers { get; } = [];
    public ObservableCollection<string> PaperSizes { get; } = [];
    public ObservableCollection<PrinterProfile> Profiles { get; } = [];

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    public PrinterProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value) || value is null)
            {
                return;
            }

            ProfileName = value.Name;
            SelectedPrinter = value.DeviceName;
            PaperSize = value.PaperSize;
            IsDefaultProfile = value.IsDefault;
            IsActiveProfile = value.IsActive;
        }
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string PaperSize
    {
        get => _paperSize;
        set => SetProperty(ref _paperSize, value);
    }

    public bool IsDefaultProfile
    {
        get => _isDefaultProfile;
        set => SetProperty(ref _isDefaultProfile, value);
    }

    public bool IsActiveProfile
    {
        get => _isActiveProfile;
        set => SetProperty(ref _isActiveProfile, value);
    }

    public AsyncCommand RefreshCommand => new(async () => await RunBusyAsync(async () =>
    {
        var printers = await _printerService.GetInstalledPrintersAsync();
        Printers.Clear();
        foreach (var printer in printers)
        {
            Printers.Add(printer);
        }

        var profiles = await _printerService.GetProfilesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        SelectedPrinter = Profiles.FirstOrDefault(p => p.IsDefault)?.DeviceName
            ?? Printers.FirstOrDefault()
            ?? string.Empty;
        StatusMessage = $"Loaded {Printers.Count} printer device(s), {Profiles.Count} profile(s).";
    }));

    public AsyncCommand SaveProfileCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            await _dialogService.AlertAsync("Printer", "Profile name is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            await _dialogService.AlertAsync("Printer", "Select a printer.");
            return;
        }

        var profile = SelectedProfile ?? new PrinterProfile { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
        profile.Name = ProfileName.Trim();
        profile.DeviceName = SelectedPrinter;
        profile.PaperSize = string.IsNullOrWhiteSpace(PaperSize) ? "A4" : PaperSize;
        profile.IsDefault = IsDefaultProfile;
        profile.IsActive = IsActiveProfile;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await _printerService.SaveProfileAsync(profile);
        await RefreshCommand.ExecuteAsync();
        await _dialogService.AlertAsync("Printer", "Profile saved.");
    }));

    public AsyncCommand SetDefaultCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            await _dialogService.AlertAsync("Printer", "Select a printer first.");
            return;
        }

        await _printerService.SetDefaultPrinterAsync(SelectedPrinter);
        await RefreshCommand.ExecuteAsync();
        await _dialogService.AlertAsync("Printer", "Default printer updated.");
    }));

    public AsyncCommand TestPrintCommand => new(async () => await RunBusyAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            await _dialogService.AlertAsync("Printer", "Select a printer.");
            return;
        }

        var jobId = await _printerService.EnqueueAsync(new PrintRequest
        {
            TemplateName = "Test",
            Content = "Mawasa Printer test page " + DateTime.UtcNow.ToString("O"),
            PrinterName = SelectedPrinter,
            ProfileName = string.IsNullOrWhiteSpace(ProfileName) ? null : ProfileName,
            PaperSize = PaperSize,
            MaxRetries = 1
        });

        await _dialogService.AlertAsync("Printer", $"Test print queued: {jobId}");
    }));
}
