using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.Test.Shared
{
    public class SearchTextTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_ShouldReturnEmpty_WhenValueIsNullOrWhiteSpace(string? value)
        {
            Assert.Equal(string.Empty, SearchText.Normalize(value));
        }

        [Theory]
        [InlineData("Saúde", "saude")]
        [InlineData("EDUCAÇÃO", "educacao")]
        [InlineData("Pão de Açúcar", "pao de acucar")]
        [InlineData("Crème Brûlée", "creme brulee")]
        public void Normalize_ShouldLowercaseAndRemoveAccents(string value, string expected)
        {
            Assert.Equal(expected, SearchText.Normalize(value));
        }

        [Fact]
        public void Normalize_ShouldTrimSurroundingWhiteSpace()
        {
            Assert.Equal("food", SearchText.Normalize("  Food  "));
        }

        [Fact]
        public void Normalize_ShouldKeepCharactersWithoutAccents()
        {
            Assert.Equal("50% off_now", SearchText.Normalize("50% OFF_now"));
        }
    }
}
