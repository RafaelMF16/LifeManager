using LifeManager.Domain.Categorization;

namespace LifeManager.Domain.Test.Categorization
{
    public class TrigramSimilarityTests
    {
        [Fact]
        public void Similarity_ShouldMatchPostgres_ForTheDocumentationExample()
        {
            // PostgreSQL: SELECT similarity('word', 'two words') → 0.36363637
            Assert.Equal(4.0 / 11, TrigramSimilarity.Similarity("word", "two words"), precision: 6);
        }

        [Fact]
        public void Similarity_ShouldBeOne_WhenTheWordsAreTheSame()
        {
            Assert.Equal(1, TrigramSimilarity.Similarity("ifood restaurante", "restaurante ifood"));
        }

        [Fact]
        public void Similarity_ShouldBeZero_WhenNothingIsShared()
        {
            Assert.Equal(0, TrigramSimilarity.Similarity("netflix", "uber"));
        }

        [Theory]
        [InlineData("", "uber")]
        [InlineData("uber", "")]
        public void Similarity_ShouldBeZero_WhenEitherSideIsEmpty(string first, string second)
        {
            Assert.Equal(0, TrigramSimilarity.Similarity(first, second));
        }

        [Fact]
        public void Trigrams_ShouldPadEachWordLikePostgres()
        {
            // PostgreSQL: SELECT show_trgm('cat') → {"  c"," ca","at ",cat}
            Assert.True(TrigramSimilarity.Trigrams("cat").SetEquals(["  c", " ca", "cat", "at "]));
        }
    }
}
