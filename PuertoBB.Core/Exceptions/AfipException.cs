namespace PuertoBB.Core.Exceptions;

public class AfipException : Exception
{
    public AfipException(string message) : base(message) { }
    public AfipException(string message, Exception inner) : base(message, inner) { }
}
