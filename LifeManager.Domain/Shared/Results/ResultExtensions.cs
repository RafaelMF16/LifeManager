namespace LifeManager.Domain.Shared.Results
{
    public static class ResultExtensions
    {
        public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func)
        {
            if (!result.IsSuccess)
                return result.Error;

            return func(result.Value);
        }

        public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> func)
        {
            if (!result.IsSuccess)
                return result.Error;

            return func(result.Value);
        }
    }
}