namespace Agent.Services;

public interface IPortManager
{
    void ReleasePort(int port);
    int? ReserveNextPort();
}

