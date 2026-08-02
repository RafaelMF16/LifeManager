namespace LifeManager.Domain.Shared.Results
{
    public enum ErrorType
    {
        None = 0,
        Validation = 1,
        NotFound = 2,
        Unauthorized = 3,
        Failure = 4,
    }
}