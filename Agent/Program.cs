using Agent.Serialization;
using Agent.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // This makes the API use your source-generated context globally
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
var systemdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/systemd/user/");

builder.Services.AddTransient<IFileNamingService, FileNamingService>();
builder.Services.AddTransient(sp => new ProcessManager(systemdPath));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var servicesApi = app.MapGroup("/services");

servicesApi.MapPost("/", async (IFormFile file, ProcessManager processRunner, IFileNamingService namingService) =>
{
    if (file.Length > 50 * 1024 * 1024)
        return Results.BadRequest("File too large. Max 50MB.");

    string appName;
    try
    {
        appName = namingService.GetSafeAppName(file.FileName);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }

    var uploadPath = namingService.GetUploadPath(appName);

    if (!Directory.Exists("slice"))
        Directory.CreateDirectory("slice");

    await using var stream = File.Create(uploadPath);
    await file.CopyToAsync(stream);

    await processRunner.CreateSystemdService(appName);

    return Results.Accepted($"{appName} Accepted");
});


servicesApi.MapGet("/", async () =>
{
});

app.Run();

