namespace BatalhaNaval.Domain.Exceptions;

public class OpponentBusyException : Exception
{
    public OpponentBusyException(string message) : base(message)
    {
    }
}