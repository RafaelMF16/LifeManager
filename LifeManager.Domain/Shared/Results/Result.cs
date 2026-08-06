using System.Diagnostics.CodeAnalysis;

namespace LifeManager.Domain.Shared.Results
{
    public record Result
    {
        [MemberNotNullWhen(false, nameof(Error))]
        public virtual bool IsSuccess { get; }

        public Error? Error { get; }

        protected Result(bool isSuccess, Error? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, null);
        public static Result Failure(Error error) => new(false, error);

        public static implicit operator Result(Error error) => Failure(error);
    }

    public record Result<T> : Result
    {
        public T? Value { get; }

        [MemberNotNullWhen(true, nameof(Value))]
        public override bool IsSuccess => base.IsSuccess;

        private Result(T value) : base(true, null) => Value = value;
        private Result(Error error) : base(false, error) { }

        public static implicit operator Result<T>(T value) => new(value);

        public static implicit operator Result<T>(Error error) => new(error);
    }
}