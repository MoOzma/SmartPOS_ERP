namespace SmartPOS_ERP.Models;

public sealed class SettingsPageViewModel
{
    public StoreSettings Store { get; set; } = new();
    public bool HasResetPin { get; set; }
}
