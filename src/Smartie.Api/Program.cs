using Smartie.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddSmartieApi();

var app = builder.Build();

await app.InitializeDatabaseAsync();
app.MapSmartieApi();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference it.
public partial class Program;
