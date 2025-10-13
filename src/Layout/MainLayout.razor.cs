using System;
using Microsoft.AspNetCore.Components;

namespace Toolbox.Layout;

public partial class MainLayout
{
    private string? currentPageTitle;

    public string? CurrentPageTitle => currentPageTitle;

    public void UpdateCurrentPageTitle(string? title)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? null : title;

        if (string.Equals(currentPageTitle, normalizedTitle, StringComparison.Ordinal))
        {
            return;
        }

        currentPageTitle = normalizedTitle;
        StateHasChanged();
    }
}
