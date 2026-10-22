namespace Stokin.Application.Common;

public record SessionContext(Guid SessionId, long UserId);
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public record PageRequest(int Page = 1, int Size = 50, string Sort = "SKU", bool Descending = false);
public record PageResult<T>(IReadOnlyList<T> Items, long Total, int Page, int Size);
public class ValidationException(string message) : Exception(message);
public class AuthorizationException(string message = "Anda tidak memiliki izin untuk tindakan ini.") : Exception(message);
public class ConcurrencyException(string message = "Data berubah. Muat ulang dan periksa kembali.") : Exception(message);
public class SessionExpiredException() : Exception("Sesi terkunci. Silakan login kembali.");
public class PostingOutcomeUnknownException(Guid requestId, Exception inner) : Exception("Hasil pembukuan belum diketahui. Periksa RequestId sebelum mencoba kembali.", inner) { public Guid RequestId { get; } = requestId; }
