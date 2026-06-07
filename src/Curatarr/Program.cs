using Curatarr.Components;
using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Services.Sonarr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CuratarrDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/ping", () => "pong");

app.MapGet("/sonarr/ping", async (SonarrClient sonarr, CancellationToken ct) =>
{
    var status = await sonarr.GetSystemStatusAsync(ct);
    return Results.Ok(status);
});

app.MapPost("/sonarr/sync/series", async (SonarrSyncService sync, CancellationToken ct) =>
{
    var count = await sync.SyncSeriesAsync(ct);
    return Results.Ok(new { synced = count });
});

app.Run();
