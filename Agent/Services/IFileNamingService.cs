namespace Agent.Services;

public interface IFileNamingService
{
    string GetSafeAppName(string fileName);
    string GetUploadPath(string appName);
}
