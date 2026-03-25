namespace WebApplication2.Exceptions;

public sealed class ConcurrencyConflictException : Exception
{
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }

    public ConcurrencyConflictException(long expectedVersion, long actualVersion)
        : base($"Concurrency conflict: expected version {expectedVersion} but found {actualVersion}.")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
