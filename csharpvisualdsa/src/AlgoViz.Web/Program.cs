using AlgoViz.Core.Runner;
using AlgoViz.Web.Components;
using AlgoViz.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ScriptRunner>();
builder.Services.AddSingleton<SampleCatalog>();
builder.Services.AddSingleton<ApiCatalog>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Warm up Roslyn and the JIT so the first real run is fast.
app.Lifetime.ApplicationStarted.Register(() =>
    _ = Task.Run(() => app.Services.GetRequiredService<ScriptRunner>().RunAsync("Viz.Log(0);")));

app.Run();
