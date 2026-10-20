using LifeManager.Domain.Categorization;

namespace LifeManager.Domain.Test.Categorization
{
    public class MerchantKeyTests
    {
        [Theory]
        [InlineData("COMPRA CARTAO DEB MC 12/03 IFOOD *RESTAURANTE 1234", "ifood restaurante")]
        [InlineData("Compra no débito - IFOOD*RESTAURANTE", "ifood restaurante")]
        [InlineData("PIX ENVIADO JOAO DA SILVA", "joao da silva")]
        [InlineData("Pix recebido - Maria Souza 12345678900", "maria souza")]
        [InlineData("PAG*UBER TRIP HELP.UBER.COM", "uber trip help")]
        [InlineData("PAGAMENTO DE BOLETO ENERGISA S/A", "energisa")]
        [InlineData("NETFLIX.COM 02/12", "netflix com")]
        [InlineData("DEB AUTOMATICO SABESP 0123456", "sabesp")]
        [InlineData("SUPERMERCADO PÃO DE AÇÚCAR LTDA SAO PAULO BR", "supermercado pao de")]
        public void From_ShouldKeepTheMerchantAndDropTheBankNoise(string description, string expected)
        {
            Assert.Equal(expected, MerchantKey.From(description));
        }

        [Fact]
        public void From_ShouldGiveTheSameKey_WhenOnlyDatesAndNumbersChange()
        {
            Assert.Equal(
                MerchantKey.From("COMPRA CARTAO 05/01 SPOTIFY 4321"),
                MerchantKey.From("COMPRA CARTAO 05/02 SPOTIFY 9876"));
        }

        [Theory]
        [InlineData("PIX RECEBIDO", "pix recebido")]
        [InlineData("TARIFA BANCARIA", "tarifa bancaria")]
        public void From_ShouldKeepTheWords_WhenTheLineIsOnlyNoise(string description, string expected)
        {
            Assert.Equal(expected, MerchantKey.From(description));
        }

        [Fact]
        public void From_ShouldFallBackToTheNumbers_WhenThereAreNoWords()
        {
            Assert.Equal("123456", MerchantKey.From("123456"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ** -- ")]
        public void From_ShouldReturnEmpty_WhenThereIsNothingToKeep(string? description)
        {
            Assert.Equal(string.Empty, MerchantKey.From(description));
        }

        [Fact]
        public void From_ShouldKeepAtMostTheFirstWords()
        {
            Assert.Equal("posto shell ipiranga", MerchantKey.From("POSTO SHELL IPIRANGA CENTRO CAMPINAS"));
        }
    }
}
