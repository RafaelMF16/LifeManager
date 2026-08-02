namespace LifeManager.Domain.Shared.Results
{
    public record Error
    {
        public string Code { get; }
        public string Message { get; }
        public ErrorType Type { get; }

        private Error(string code, string message, ErrorType type)
        {
            Code = code;
            Message = message;
            Type = type;
        }

        public static Error Validation(string code, string message)
            => new(code, message, ErrorType.Validation);

        public static Error NotFound(string code, string message)
            => new(code, message, ErrorType.NotFound);

        public static Error Unauthorized(string code, string message)
            => new(code, message, ErrorType.Unauthorized);

        public static Error Failure(string code, string message)
            => new(code, message, ErrorType.Failure);
    }
}