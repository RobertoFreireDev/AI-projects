using SqliteViz.Core;
using SqliteViz.Core.Session;
using SqliteViz.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One private in-memory database per circuit.
builder.Services.AddScoped<DbSession>();

var app = builder.Build();

// Fail fast when the bundled SQLite is too old for the samples.
try
{
    var sqlite = SqliteEnvironment.CheckOrThrow();
    app.Logger.LogInformation("SQLite {Version} (math functions: {Math}, fts5: {Fts5}, rtree: {Rtree})",
        sqlite.Version, sqlite.NativeMathFunctions ? "built in" : "registered by SqliteViz", sqlite.HasFts5, sqlite.HasRtree);
}
catch (Exception e)
{
    app.Logger.LogCritical("{Message}", e.Message);
    throw;
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

app.Run();
