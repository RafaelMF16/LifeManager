using LifeManager.Domain.StatementImports;

namespace LifeManager.Domain.Test.StatementImports
{
    public class OwnTransferDetectorTests
    {
        private const string Holder = "Fulano Beltrano Ciclano";

        [Theory]
        [InlineData("Pix enviado: \"Cp :18236120-Fulano Beltrano Ciclano\"")]
        [InlineData("Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\"")]
        [InlineData("Transferência recebida pelo Pix - FULANO BELTRANO CICLANO - •••.000.000-•• - BANCO INTER (0077) Agência: 1 Conta: 1234-5")]
        [InlineData("Transferência enviada pelo Pix - Fulano Beltrano Ciclano - •••.000.000-•• - BANCO INTER (0077) Agência: 1 Conta: 1234-5")]
        [InlineData("TED RECEBIDA FULANO BELTRANO CICLANO")]
        public void IsOwnTransfer_ShouldFindATransferInTheHoldersName(string description)
        {
            Assert.True(OwnTransferDetector.For(Holder).IsOwnTransfer(description));
        }

        [Fact]
        public void IsOwnTransfer_ShouldFindTheHolder_WhenTheBankWritesMoreNamesThanTheAccount()
        {
            Assert.True(OwnTransferDetector.For("Fulano Ciclano").IsOwnTransfer("Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\""));
        }

        [Fact]
        public void IsOwnTransfer_ShouldIgnoreAccentsCaseAndNameParticles()
        {
            var detector = OwnTransferDetector.For("José da Silva dos Santos");

            Assert.True(detector.IsOwnTransfer("PIX RECEBIDO JOSE SILVA SANTOS"));
            Assert.True(detector.IsOwnTransfer("Transferência enviada pelo Pix - José da Silva dos Santos - BANCO X"));
        }

        [Theory]
        [InlineData("Transferência Recebida - Outra Pessoa Qualquer - •••.000.000-•• - NU PAGAMENTOS - IP (0260)")]
        [InlineData("Pix enviado: \"Cp :00360305-Fulano Alcantara\"")]
        [InlineData("Pix recebido: \"Cp :18236120-Ciclano Beltrano Fulano\"")]
        [InlineData("Estorno: \"CDB PORQUINHO FULANO BELTRANO CICLANO\"")]
        [InlineData("Compra no debito: \"FULANO BELTRANO CICLANO BARBEARIA\"")]
        [InlineData("Pixel Store Fulano Beltrano Ciclano")]
        [InlineData("Pix enviado: \"Cp :18236120-Fulanobeltrano Ciclano\"")]
        [InlineData("")]
        [InlineData(null)]
        public void IsOwnTransfer_ShouldFindNothing_WhenTheLineIsNotATransferInTheHoldersName(string? description)
        {
            Assert.False(OwnTransferDetector.For(Holder).IsOwnTransfer(description));
        }

        [Theory]
        [InlineData("Fulano")]
        [InlineData("Fulano de")]
        [InlineData("Jo da Silva")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void IsOwnTransfer_ShouldFindNothing_WhenTheHoldersNameHasLessThanTwoWords(string? holder)
        {
            Assert.False(OwnTransferDetector.For(holder).IsOwnTransfer("Pix recebido: \"Cp :90400888-FULANO DE TAL\""));
            Assert.False(OwnTransferDetector.For(holder).IsOwnTransfer("Pix recebido: \"Cp :90400888-JO DA SILVA\""));
        }
    }
}
