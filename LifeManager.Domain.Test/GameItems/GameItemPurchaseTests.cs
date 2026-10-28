using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.GameItems
{
    public class GameItemPurchaseTests
    {
        private static readonly DateOnly Today = new(2026, 10, 28);
        private static readonly DateTimeOffset Now = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

        private static GameItemPurchase Potion()
            => GameItemPurchase.Create(new UserId(1), GameItemKind.HpPotion, costPaid: 40, hpRestored: 25, Today, Now);

        [Fact]
        public void Create_ShouldKeepWhatWasPaidAndHealed()
        {
            var purchase = Potion();

            Assert.Null(purchase.Id);
            Assert.Equal(1, purchase.UserId.Value);
            Assert.Equal(GameItemKind.HpPotion, purchase.Kind);
            Assert.Equal(40, purchase.CostPaid);
            Assert.Equal(25, purchase.HpRestored);
            Assert.Equal(Today, purchase.PurchasedOn);
            Assert.Equal(Now, purchase.PurchasedAt);
            Assert.False(purchase.IsUndone);
        }

        [Fact]
        public void Undo_ShouldMarkItUndone_OnTheSameDay()
        {
            var purchase = Potion();
            Assert.True(purchase.CanUndo(Today));

            var result = purchase.Undo(Today, Now.AddHours(1));

            Assert.True(result.IsSuccess);
            Assert.Equal(Now.AddHours(1), purchase.UndoneAt);
            Assert.False(purchase.CanUndo(Today));
        }

        [Fact]
        public void Undo_ShouldFail_OnALaterDay()
        {
            var purchase = Potion();

            var result = purchase.Undo(Today.AddDays(1), Now.AddDays(1));

            Assert.Equal(GameItemErrors.UndoOutsideWindow, result.Error);
            Assert.False(purchase.IsUndone);
        }

        [Fact]
        public void Undo_ShouldFail_WhenAlreadyUndone()
        {
            var purchase = GameItemPurchase.FromPersistence(3, 1, GameItemKind.StreakFreeze, 150, 0, Today, Now, undoneAt: Now);

            var result = purchase.Undo(Today, Now.AddHours(1));

            Assert.Equal(GameItemErrors.AlreadyUndone, result.Error);
            Assert.Equal(Now, purchase.UndoneAt);
            Assert.Equal(3, purchase.Id!.Value);
        }
    }
}
