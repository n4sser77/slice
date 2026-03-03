namespace Agent.Models;

public record AppService(int Id, string Title, DateOnly? DateCreated = null, bool IsRunning = false);

