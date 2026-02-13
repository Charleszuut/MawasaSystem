namespace MawasaProject.Presentation.Shell;

public partial class AppShell : Microsoft.Maui.Controls.Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("login", typeof(Views.Pages.LoginPage));
        Routing.RegisterRoute("backup", typeof(Views.Pages.BackupPage));
        Routing.RegisterRoute("printer-settings", typeof(Views.Pages.PrinterSettingsPage));
        Routing.RegisterRoute("print-queue", typeof(Views.Pages.PrintQueuePage));
        Routing.RegisterRoute("receipt", typeof(Views.Pages.ReceiptPage));
        Routing.RegisterRoute("invoice", typeof(Views.Pages.InvoicePage));
    }
}
