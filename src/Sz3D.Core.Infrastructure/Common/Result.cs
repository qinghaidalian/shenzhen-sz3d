namespace Sz3D.Core.Infrastructure.Common;

public sealed record Result(bool IsSuccess, string Error = "")
{
    public static Result Ok()              => new(true);
    public static Result Fail(string err)  => new(false, err);
}

public sealed record Result<T>(bool IsSuccess, T? Value = default, string Error = "")
{
    public static Result<T> Ok(T value)    => new(true, value);
    public static Result<T> Fail(string e) => new(false, default, e);
}
