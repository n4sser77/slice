using Agent.Cli.Configuration;

namespace Agent.Cli.Tests;

public sealed class SliceConfigurationParserTests : IDisposable
{
  private readonly string _directory = Path.Combine(
      Path.GetTempPath(),
      $"slice-config-parser-{Guid.NewGuid():N}");

  public SliceConfigurationParserTests() => Directory.CreateDirectory(_directory);

  public void Dispose() => Directory.Delete(_directory, recursive: true);

  [Fact]
  public void Parse_ReadsDotenvSyntaxWithoutExpandingVariables()
  {
    var path = WriteFile("""
        # Application configuration
        ConnectionStrings:Postgres="Host=db;Password=p\"a\\ss"
        URL=https://example.test/path#fragment
        MESSAGE='line\nnext' # comment
        EMPTY=
        REFERENCE=$URL
        """);

    var result = SliceConfigurationParser.Parse(path, []);

    Assert.Equal("Host=db;Password=p\"a\\ss", result.Values["ConnectionStrings:Postgres"]);
    Assert.Equal("https://example.test/path#fragment", result.Values["URL"]);
    Assert.Equal("line\nnext", result.Values["MESSAGE"]);
    Assert.Equal(string.Empty, result.Values["EMPTY"]);
    Assert.Equal("$URL", result.Values["REFERENCE"]);
  }

  [Fact]
  public void Parse_CommandLineOverridesFileCaseInsensitively()
  {
    var path = WriteFile("DATABASE_URL=from-file");

    var result = SliceConfigurationParser.Parse(path, ["database_url=from-flag"]);

    Assert.Single(result.Values);
    Assert.Equal("from-flag", result.Values["DATABASE_URL"]);
  }

  [Fact]
  public void Parse_EmptyFileIsAnExplicitEmptyConfiguration()
  {
    var result = SliceConfigurationParser.Parse(WriteFile("# intentionally empty"), []);

    Assert.Empty(result.Values);
  }

  [Theory]
  [InlineData("export KEY=value")]
  [InlineData("KEY")]
  [InlineData("KEY=\"unterminated")]
  [InlineData("KEY=\"value\" trailing")]
  public void Parse_RejectsUnsupportedSyntax(string content)
  {
    var error = Assert.Throws<FormatException>(
        () => SliceConfigurationParser.Parse(WriteFile(content), []));

    Assert.DoesNotContain("secret-value", error.Message);
  }

  [Fact]
  public void Parse_RejectsDuplicateKeysWithinOneSource()
  {
    var path = WriteFile("Key=one\nKEY=two");

    var error = Assert.Throws<FormatException>(
        () => SliceConfigurationParser.Parse(path, []));

    Assert.Contains("duplicate key", error.Message);
  }

  private string WriteFile(string content)
  {
    var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.env.slice");
    File.WriteAllText(path, content);
    return path;
  }
}
