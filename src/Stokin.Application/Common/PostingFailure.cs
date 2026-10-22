namespace Stokin.Application.Common;

public static class PostingFailure
{
    public static bool IsDefinite(Exception error) => error is ValidationException or AuthorizationException or ConcurrencyException or ArgumentException or OverflowException;
}
