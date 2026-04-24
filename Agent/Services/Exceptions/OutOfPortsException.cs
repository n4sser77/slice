namespace Agent.Services.Exceptions;

public class OutOfPortsException : Exception
{
  public OutOfPortsException() : base("No available ports left in the allocated range (5001-5050).") { }
  public OutOfPortsException(string message) : base(message) { }
  public OutOfPortsException(string message, Exception inner) : base(message, inner) { }
}

