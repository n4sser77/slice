using Agent.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();

var systemdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/systemd/user/");

builder.Services.AddTransient<IFileNamingService, FileNamingService>();
builder.Services.AddTransient(sp => new ProcessRunner(
    sp.GetRequiredService<IFileNamingService>(),
    systemdPath));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var servicesApi = app.MapGroup("/services");

servicesApi.MapPost("/", async (IFormFile file, ProcessRunner processRunner) =>
{
    if (file.Length > 50 * 1024 * 1024)
        return Results.BadRequest("File too large. Max 50MB.");

    IFileNamingService namingService = new FileNamingService();
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

    processRunner.CreateSystemdService(appName);

    return Results.Accepted($"{appName} Accepted");
});

app.Run();
