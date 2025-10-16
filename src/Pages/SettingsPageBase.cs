using Microsoft.AspNetCore.Components;
using Toolbox.Components;
using Toolbox.Helpers;
using Toolbox.Layout;

namespace Toolbox.Pages;

public abstract class SettingsPageBase : ComponentBase
{
    [Inject]
    protected LocalStorageHelper LocalStorage { get; set; } = default!;

    [CascadingParameter]
    protected MainLayout? Layout { get; set; }
    
    protected HelpDialog? HelpDialog { get; set; }

    protected string GetHelpButtonLabel(string controlLabel) => HelpDialog.GetButtonLabel(controlLabel);

    protected Task ShowHelpAsync(string helpKey, string helpTitle) => HelpDialog?.ShowAsync(helpKey, helpTitle) ?? Task.CompletedTask;

    protected void UpdatePageTitle(string title) => Layout?.UpdateCurrentPageTitle(title);
}
