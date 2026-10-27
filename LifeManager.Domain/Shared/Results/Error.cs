namespace LifeManager.Domain.Shared.Results
{
    public record Error
    {
        public string Code { get; }
        public string Message { get; }
        public ErrorType Type { get; }

        /// <summary>
        /// The position of the item the error is about, when a request sends a list and one item fails (a name among the
        /// account holders). Null for every other error, and then left out of the response body.
        /// </summary>
        public int? Index { get; private init; }

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

        public static Error Conflict(string code, string message)
            => new(code, message, ErrorType.Conflict);

        /// <summary>The same error, about the item at <paramref name="index"/> of the list that was sent.</summary>
        public Error AtIndex(int index) => this with { Index = index };
    }
}