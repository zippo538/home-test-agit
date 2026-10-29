namespace AccessRequestHub.Web.Services;

/// <summary>400: input tidak valid. Errors berisi pesan per-field.</summary>
public class ValidationFailedException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base("Validation failed.") => Errors = new Dictionary<string, string[]>(errors);

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}

/// <summary>403: user tidak berhak melakukan aksi.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>404: data tidak ada, atau tidak boleh diketahui keberadaannya oleh user.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>409: konflik state. Code: already_final, not_your_stage, stale_version, idempotency_mismatch.</summary>
public class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message) : base(message) => Code = code;
}
