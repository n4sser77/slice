namespace Slice.Common.Models;

public class AppService
{
    public AppService(int Id, string Title, DateOnly? DateCreated = null, bool IsRunning = false)
    {
        id = Id;
        title = Title;
        dateCreated = DateCreated;
        isRunning = IsRunning;
    }
    private readonly int id;
    private readonly string title;
    private readonly DateOnly? dateCreated;
    private readonly bool isRunning;

}
