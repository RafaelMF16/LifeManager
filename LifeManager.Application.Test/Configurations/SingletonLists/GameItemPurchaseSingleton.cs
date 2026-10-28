using LifeManager.Domain.GameItems;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class GameItemPurchaseSingleton : List<GameItemPurchase>
    {
        private GameItemPurchaseSingleton() { }

        private static readonly Lazy<GameItemPurchaseSingleton> lazy = new(() => new GameItemPurchaseSingleton());

        public static GameItemPurchaseSingleton Instance => lazy.Value;
    }
}
