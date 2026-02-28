using Agent.Services;

var builder = WebApplication.CreateSlimBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var systemdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/systemd/user/");

builder.Services.AddTransient(sp => new ProcessRunner(systemdPath));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var servicesApi = app.MapGroup("/services");

servicesApi.MapPost("/", async (IFormFile file, ProcessRunner _pr) =>
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension != ".dll")
        return Results.BadRequest("Only .dll files are accepted.");

    if (file.Length > 100 * 1024 * 1024)
        return Results.BadRequest("File too large. Max 100MB.");

    string appName = "slice-" + Path.GetFileNameWithoutExtension(file.FileName);

    var path = Path.Combine("slice", appName);

    if (!Directory.Exists("slice"))
        Directory.CreateDirectory("slice");

    using var stream = File.Create(path + extension);
    await file.CopyToAsync(stream);

    _pr.CreateSystemdService(appName);

    return Results.Accepted($"{appName} Accepted");
});

app.Run();
