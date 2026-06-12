using Curatarr.Components;
using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Endpoints;
using Curatarr.Services.Destination;
using Curatarr.Services.Diff;
using Curatarr.Services.Scheduling;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Curatarr");
}

builder.Services.AddDbContextFactory<CuratarrDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Curatarr")));

builder.Services.Configure<SonarrOptions>(
    builder.Configuration.GetSection(SonarrOptions.SectionName));

builder.Services.AddHttpClient<SonarrClient>((sp, client) =>
{
    var sonarr = sp.GetRequiredService<IOptions<SonarrOptions>>().Value;
    client.BaseAddress = new Uri(sonarr.Url);
    client.DefaultRequestHeaders.Add("X-Api-Key", sonarr.ApiKey);
});

builder.Services.AddScoped<SonarrSyncService>();

builder.Services.Configure<DestinationOptions>(
    builder.Configuration.GetSection(DestinationOptions.SectionName));

builder.Services.AddSingleton<DestinationScanner>();
builder.Services.AddScoped<DestinationSyncService>();

builder.Services.Configure<SourceOptions>(
    builder.Configuration.GetSection(SourceOptions.SectionName));
builder.Services.Configure<SubtitleOptions>(
    builder.Configuration.GetSection(SubtitleOptions.SectionName));
builder.Services.AddScoped<SubtitleSyncService>();

builder.Services.AddScoped<SeriesDiffService>();

builder.Services.AddSingleton(SyncScheduledTask.Create(TimeSpan.FromHours(1)));
builder.Services.AddSingleton<ScheduledTaskRegistry>();
builder.Services.AddHostedService<ScheduledTaskHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CuratarrDbContext>>();
    using var db = factory.CreateDbContext();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthEndpoints();
app.MapSonarrEndpoints();
app.MapSyncEndpoints();

await app.RunAsync();
