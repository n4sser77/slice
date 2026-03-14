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
builder.Services.AddSingleton<IPortManager, PortManager>();
builder.Services.AddTransient(sp =>
        new ProcessManager(systemdPath, sp.GetRequiredService<IPortManager>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}



app.MapPost("v1/services", [RequestSizeLimit(100_000_000)] async (IFormFile file, ProcessManager processRunner, IFileNamingService namingService) =>
{
    try
    {
        string appSafePath = namingService.GetSafeAppName(file.FileName);
        string dllName = Path.GetFileNameWithoutExtension(file.FileName);
        var uploadPath = namingService.GetUploadPath(appSafePath);
        Directory.CreateDirectory(uploadPath);
        var z = new ZipExtractor();
        await z.ReadAndUnzip(file.OpenReadStream(), uploadPath);

        var dllPath = Path.Combine(Path.GetFullPath(uploadPath), dllName + ".dll");
        if (!File.Exists(dllPath))
            return Results.BadRequest($"No runnable DLL '{dllName}.dll' found in uploaded archive.");

        await processRunner.CreateSystemdService(appSafePath, dllName);

        return Results.Accepted($"{appSafePath} Accepted");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).DisableAntiforgery();



app.Run();

