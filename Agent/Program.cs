using Agent.Serialization;
using Agent.Services;
using Microsoft.AspNetCore.Mvc;

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

servicesApi.MapPost("/", [RequestSizeLimit(100_000_000)] async (IFormFile file, ProcessManager processRunner, IFileNamingService namingService) =>
{
    try
    {
        string appSafePath = namingService.GetSafeAppName(file.FileName);
        var z = new ZipExtractor();
        z.ReadAndUnzip(file.OpenReadStream(), appSafePath);

        var uploadPath = namingService.GetUploadPath(appSafePath);

        if (!Directory.Exists("slice"))
            Directory.CreateDirectory("slice");

        await using var stream = File.Create(uploadPath);
        await file.CopyToAsync(stream);

        await processRunner.CreateSystemdService(appSafePath);

        return Results.Accepted($"{appSafePath} Accepted");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});


// servicesApi.MapGet("/", async () =>
// {
// });

app.Run();

