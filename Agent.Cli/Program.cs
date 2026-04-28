using Agent.Cli;
using Agent.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

var config = CliConfig.Default;

var services = new ServiceCollection()
    .AddSingleton(new HttpClient { BaseAddress = config.BaseAddress, Timeout = TimeSpan.FromSeconds(30) })
    .BuildServiceProvider();

var httpClient = services.GetRequiredService<HttpClient>();

var root = new RootCommand("slice — deploy and manage .NET services");
DeployServiceCommand.Register(root, httpClient);
GetServicesCommand.Register(root, httpClient);
GetServiceStatusCommand.Register(root, httpClient);

return await root.Parse(args).InvokeAsync();
