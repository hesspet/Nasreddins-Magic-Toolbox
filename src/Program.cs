using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using Toolbox;
using Toolbox.Helpers;
using Toolbox.Settings;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var germanCulture = CultureInfo.GetCultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = germanCulture;
CultureInfo.DefaultThreadCurrentUICulture = germanCulture;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<LocalStorageHelper>();
builder.Services.AddScoped<IndexedDbHelper>();
builder.Services.AddScoped<DeckBootstrapper>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddSingleton<HelpContentProvider>();
builder.Services.AddScoped<InMemoryLogService>();

await builder.Build().RunAsync();
