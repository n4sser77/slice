using Slice.Common.Models;

namespace Agent.Tests;

public class ApplicationConfigurationValidatorTests
{
  [Theory]
  [InlineData("ConnectionStrings:Postgres")]
  [InlineData("Serilog:WriteTo:0:Name")]
  [InlineData("DATABASE_URL")]
  public void Validate_AcceptsCanonicalKeys(string key)
  {
    var configuration = Configuration((key, "value"));

    Assert.Null(ApplicationConfigurationValidator.Validate(configuration));
  }

  [Theory]
  [InlineData("ConnectionStrings__Postgres")]
  [InlineData("ConnectionStrings::Postgres")]
  [InlineData("0Invalid")]
  [InlineData("Invalid-Key")]
  [InlineData("ASPNETCORE_URLS")]
  [InlineData("DOTNET:Root")]
  public void Validate_RejectsInvalidOrReservedKeys(string key)
  {
    var configuration = Configuration((key, "value"));

    Assert.NotNull(ApplicationConfigurationValidator.Validate(configuration));
  }

  [Fact]
  public void Validate_RejectsCaseInsensitiveCollisions()
  {
    var configuration = Configuration(("ApiKey", "one"), ("APIKEY", "two"));

    Assert.Contains("differ only by casing", ApplicationConfigurationValidator.Validate(configuration));
  }

  [Fact]
  public void Validate_DoesNotIncludeSecretValuesInErrors()
  {
    var configuration = Configuration(("INVALID-KEY", "secret-value"));

    Assert.DoesNotContain("secret-value", ApplicationConfigurationValidator.Validate(configuration));
  }

  [Fact]
  public void Validate_RejectsNullValuesFromUntrustedJson()
  {
    var values = new Dictionary<string, string> { ["KEY"] = null! };
    var configuration = new ApplicationConfiguration(
        ApplicationConfiguration.CurrentSchemaVersion,
        values);

    Assert.Contains("cannot be null", ApplicationConfigurationValidator.Validate(configuration));
  }

  private static ApplicationConfiguration Configuration(params (string Key, string Value)[] entries) =>
      new(ApplicationConfiguration.CurrentSchemaVersion, entries.ToDictionary());
}
