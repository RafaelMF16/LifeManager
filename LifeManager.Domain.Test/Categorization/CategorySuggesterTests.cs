using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Test.Categorization
{
    public class CategorySuggesterTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);

        private const int Restaurants = 1;
        private const int Market = 2;
        private const int Salary = 3;
        private const int Savings = 4;
        private const int Transport = 5;

        private static CategoryEvidence Rule(string key, int categoryId, int hits, MoneyFlowType type = MoneyFlowType.Expense, DateOnly? lastUsedOn = null)
            => new(key, type, RuleAction.Categorize, new CategoryId(categoryId), hits, lastUsedOn ?? Today, IsRule: true);

        private static CategoryEvidence IgnoreRule(string key, int hits, MoneyFlowType type = MoneyFlowType.Expense)
            => new(key, type, RuleAction.Ignore, null, hits, Today, IsRule: true);

        private static CategoryEvidence History(string key, int categoryId, int count, MoneyFlowType type = MoneyFlowType.Expense)
            => new(key, type, RuleAction.Categorize, new CategoryId(categoryId), count, Today, IsRule: false);

        private static CategorySuggestion? Suggest(string key, bool isIncome, params CategoryEvidence[] evidence)
            => new CategorySuggester(evidence, Today).Suggest(key, isIncome);

        [Fact]
        public void Suggest_ShouldReturnNull_WhenThereIsNoEvidence()
        {
            Assert.Null(Suggest("ifood restaurante", isIncome: false));
        }

        [Fact]
        public void Suggest_ShouldIgnoreWhatWasLearnedAboutTheMonth()
        {
            var suggestion = Suggest(
                "salario empresa xpto",
                true,
                Rule("salario empresa xpto", Salary, hits: 2, type: MoneyFlowType.Income),
                new CategoryEvidence("salario empresa xpto", MoneyFlowType.Income, RuleAction.NextMonth, null, 9, Today, IsRule: true),
                new CategoryEvidence("salario empresa xpto", MoneyFlowType.Income, RuleAction.SameMonth, null, 9, Today, IsRule: true));

            Assert.Equal((RuleAction.Categorize, new CategoryId(Salary), SuggestionConfidence.High), (suggestion!.Action, suggestion.CategoryId, suggestion.Confidence));
        }

        [Fact]
        public void Suggest_ShouldBeHighConfidence_WhenTheSameMerchantWentToOneCategoryMoreThanOnce()
        {
            var suggestion = Suggest("ifood restaurante", false, Rule("ifood restaurante", Restaurants, hits: 2));

            Assert.Equal(new CategorySuggestion(RuleAction.Categorize, MoneyFlowType.Expense, new CategoryId(Restaurants), SuggestionSource.Rule, SuggestionConfidence.High, 2), suggestion);
        }

        [Fact]
        public void Suggest_ShouldBeMediumConfidence_WhenTheMerchantWasFiledOnlyOnce()
        {
            var suggestion = Suggest("ifood restaurante", false, Rule("ifood restaurante", Restaurants, hits: 1));

            Assert.Equal(SuggestionConfidence.Medium, suggestion!.Confidence);
        }

        [Fact]
        public void Suggest_ShouldBeMediumConfidence_WhenTheMerchantIsSplitBetweenCategories()
        {
            var suggestion = Suggest("ifood restaurante", false,
                Rule("ifood restaurante", Restaurants, hits: 3),
                Rule("ifood restaurante", Market, hits: 2));

            Assert.Equal(new CategoryId(Restaurants), suggestion!.CategoryId);
            Assert.Equal(SuggestionConfidence.Medium, suggestion.Confidence);
        }

        [Fact]
        public void Suggest_ShouldComeFromHistory_WhenOnlyTransactionsMatch()
        {
            var suggestion = Suggest("mercado", false, History("mercado", Market, count: 5));

            Assert.Equal(SuggestionSource.History, suggestion!.Source);
            Assert.Equal(SuggestionConfidence.High, suggestion.Confidence);
            Assert.Equal(new CategoryId(Market), suggestion.CategoryId);
        }

        [Fact]
        public void Suggest_ShouldWeighALearnedRuleAboveTransactions()
        {
            var suggestion = Suggest("mercado", false,
                History("mercado", Restaurants, count: 3),
                Rule("mercado", Market, hits: 2));

            Assert.Equal(new CategoryId(Market), suggestion!.CategoryId);
            Assert.Equal(SuggestionSource.Rule, suggestion.Source);
        }

        [Fact]
        public void Suggest_ShouldPreferRecentDecisions()
        {
            var suggestion = Suggest("ifood restaurante", false,
                Rule("ifood restaurante", Market, hits: 3, lastUsedOn: Today.AddYears(-2)),
                Rule("ifood restaurante", Restaurants, hits: 2));

            Assert.Equal(new CategoryId(Restaurants), suggestion!.CategoryId);
        }

        [Fact]
        public void Suggest_ShouldOnlyUseDecisionsInTheLinesDirection()
        {
            var evidence = Rule("transferencia joao", Restaurants, hits: 4);

            Assert.Null(Suggest("transferencia joao", isIncome: true, evidence));
            Assert.NotNull(Suggest("transferencia joao", isIncome: false, evidence));
        }

        [Fact]
        public void Suggest_ShouldUseIncomeDecisions_ForMoneyComingIn()
        {
            var suggestion = Suggest("empresa xpto", true, Rule("empresa xpto", Salary, hits: 6, type: MoneyFlowType.Income));

            Assert.Equal(MoneyFlowType.Income, suggestion!.Type);
            Assert.Equal(new CategoryId(Salary), suggestion.CategoryId);
        }

        [Fact]
        public void Suggest_ShouldTurnAnOutgoingLineIntoAnInvestment_WhenThatIsWhatTheUserDid()
        {
            var suggestion = Suggest("aplicacao cdb", false, Rule("aplicacao cdb", Savings, hits: 3, type: MoneyFlowType.Investment));

            Assert.Equal(MoneyFlowType.Investment, suggestion!.Type);
        }

        [Fact]
        public void Suggest_ShouldSuggestIgnoring_WhenTheUserLeftTheMerchantOut()
        {
            var suggestion = Suggest("joao da silva", false, IgnoreRule("joao da silva", hits: 3));

            Assert.Equal(RuleAction.Ignore, suggestion!.Action);
            Assert.Null(suggestion.CategoryId);
        }

        [Fact]
        public void Suggest_ShouldUseASimilarMerchant_WhenTheKeyIsNew()
        {
            var suggestion = Suggest("ifood restaurant", false, Rule("ifood restaurante", Restaurants, hits: 4));

            Assert.Equal(new CategoryId(Restaurants), suggestion!.CategoryId);
            Assert.Equal(SuggestionSource.Similar, suggestion.Source);
            Assert.Equal(SuggestionConfidence.Medium, suggestion.Confidence);
        }

        [Fact]
        public void Suggest_ShouldBeLowConfidence_WhenSimilarMerchantsDisagree()
        {
            var suggestion = Suggest("uber", false,
                Rule("uber trip", Transport, hits: 2),
                Rule("uber eats", Restaurants, hits: 2));

            Assert.Equal(SuggestionConfidence.Low, suggestion!.Confidence);
            Assert.Equal(SuggestionSource.Similar, suggestion.Source);
        }

        [Fact]
        public void Suggest_ShouldNeverIgnoreBecauseOfASimilarMerchant()
        {
            Assert.Null(Suggest("joao silveira", false, IgnoreRule("joao silva", hits: 5)));
        }

        [Fact]
        public void Suggest_ShouldIgnoreMerchantsThatAreNotSimilarEnough()
        {
            Assert.Null(Suggest("netflix", false, Rule("spotify", Restaurants, hits: 5)));
        }
    }
}
