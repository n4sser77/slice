using Agent.Services;

namespace Agent.Tests;

public class FileNamingServiceTests
{
    private readonly FileNamingService _sut = new();

    [Theory]
    [InlineData("MyApp.dll", "slice-myapp")]
    [InlineData("test.DLL", "slice-test")]
    [InlineData("hello-world.dll", "slice-hello-world")]
    [InlineData("app123.dll", "slice-app123")]
    public void GetSafeAppName_ValidDll_ReturnsSafeName(string fileName, string expected)
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
    [InlineData("my app.dll")]
    [InlineData("my@app.dll")]
    [InlineData("my#app.dll")]
    [InlineData("../etc/passwd.dll")]
    [InlineData("my..app.dll")]
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
        var expected = Path.Combine("slice", "slice-myapp.dll");

        var result = _sut.GetUploadPath(appName);

        Assert.Equal(expected, result);
    }
}
