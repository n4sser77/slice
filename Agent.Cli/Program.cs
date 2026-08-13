using Agent.Cli;
using Agent.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Slice.Cli.Auth;
using System.CommandLine;

var config = CliConfig.Default;
const string clientName = "SliceClient";
var services = new ServiceCollection();

services.AddTransient<ApiKeyHandler>();

services.AddHttpClient(clientName, client =>
{
  client.BaseAddress = config.BaseAddress;
  client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<ApiKeyHandler>();

var sp = services.BuildServiceProvider();
var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient(clientName);

var root = new RootCommand("slice — deploy and manage .NET services");
DeployServiceCommand.Register(root, httpClient, config);
GetServicesCommand.Register(root, httpClient);
GetServiceStatusCommand.Register(root, httpClient);
StopServiceCommand.Register(root, httpClient);
RemoveServiceCommand.Register(root, httpClient);

return await root.Parse(args).InvokeAsync();
