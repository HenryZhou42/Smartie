using Smartie.Shared.Services;
using Smartie.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ISmartieApiEndpointProvider>(_ =>
    new FixedSmartieApiEndpointProvider(
        new Uri((builder.Configuration["SmartieApi:BaseUrl"] ?? "http://localhost:5220").TrimEnd('/') + "/")));
builder.Services.AddHttpClient<SmartieApiClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IDocumentFilePickerService, WebDocumentFilePickerService>();
builder.Services.AddSingleton<INativeKnowledgeDropBridge, NullNativeKnowledgeDropBridge>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(SmartieApiClient).Assembly);

app.Run();
