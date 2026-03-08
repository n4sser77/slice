using Agent.Services;

namespace Agent.Tests;

public class FileNamingServiceTests
{
    private readonly FileNamingService _sut = new();

    [Theory]
    [InlineData("MyApp.zip", "slice-myapp")]
    [InlineData("test.ZIP", "slice-test")]
    [InlineData("hello-world.zip", "slice-hello-world")]
    [InlineData("app123.zip", "slice-app123")]
    public void GetSafeAppName_ValidZip_ReturnsSafeName(string fileName, string expected)
    {
        var result = _sut.GetSafeAppName(fileName);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("app.exe")]
    [InlineData("app.txt")]
    [InlineData("app")]
    [InlineData(".dll")]
    public void GetSafeAppName_InvalidExtension_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => _sut.GetSafeAppName(fileName));
    }

    [Theory]
    [InlineData("my app.zip")]
    [InlineData("my@app.zip")]
    [InlineData("my#app.zip")]
    [InlineData("../etc/passwd.zip")]
    [InlineData("my..app.zip")]
    public void GetSafeAppName_SpecialCharacters_RemovesThem(string fileName)
    {
        var result = _sut.GetSafeAppName(fileName);

        Assert.DoesNotContain(" ", result);
        Assert.DoesNotContain("@", result);
        Assert.DoesNotContain("#", result);
        Assert.DoesNotContain("..", result);
        Assert.StartsWith("slice-", result);
    }

    [Fact]
    public void GetSafeAppName_EmptyAfterSanitization_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.GetSafeAppName("....dll"));
    }

    [Fact]
    public void GetUploadPath_ReturnsCorrectPath()
    {
        var appName = "slice-myapp";
        var expected = Path.Combine("slice", "slice-myapp");

        var result = _sut.GetUploadPath(appName);

        Assert.Equal(expected, result);
    }
}
