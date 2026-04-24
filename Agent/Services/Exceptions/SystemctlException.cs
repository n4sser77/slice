namespace Agent.Services.Exceptions;

public sealed class SystemctlException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
  public SystemctlException() : this(null, null) { }
}
