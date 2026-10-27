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
        public void IsOwnTransfer_ShouldFindTheHolder_WhenTheBankWritesMoreNamesThanAThreeWordName()
        {
            Assert.True(OwnTransferDetector.For(Holder).IsOwnTransfer("Pix recebido: \"Cp :90400888-FULANO BELTRANO SOUZA CICLANO\""));
        }

        [Fact]
        public void IsOwnTransfer_ShouldFindATwoWordName_OnlyWhenTheWordsAreTogether()
        {
            var detector = OwnTransferDetector.For("Fulano Ciclano");

            Assert.True(detector.IsOwnTransfer("Pix enviado: \"Cp :18236120-Fulano Ciclano\""));
            Assert.True(detector.IsOwnTransfer("PIX RECEBIDO FULANO DE CICLANO"));
            Assert.False(detector.IsOwnTransfer("Pix enviado: \"Cp :18236120-Fulano Beltrano Ciclano\""));
        }

        [Theory]
        [InlineData("Fulano Ciclano")]
        [InlineData(Holder)]
        public void IsOwnTransfer_ShouldFindNothing_WhenTheTransferNamesFewerWordsThanTheHolder(string holder)
        {
            Assert.False(OwnTransferDetector.For(holder).IsOwnTransfer("Pix"));
            Assert.False(OwnTransferDetector.For(holder).IsOwnTransfer("TED 12"));
            Assert.False(OwnTransferDetector.For(holder).IsOwnTransfer("PIX ENVIADO FULANO"));
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

        [Theory]
        [InlineData("Pix enviado: \"Cp :18236120-RAFAEL MOREIRA FERREIRA\"")]
        [InlineData("TED RECEBIDA EMPRESA X LTDA")]
        [InlineData("Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\"")]
        public void IsOwnTransfer_ShouldFindATransferInAnyOfTheHoldersNames(string description)
        {
            var detector = OwnTransferDetector.For(["Rafael Moreira Ferreira", "Empresa X Ltda", Holder]);

            Assert.True(detector.IsOwnTransfer(description));
        }

        [Fact]
        public void IsOwnTransfer_ShouldKeepTheOtherHolders_WhenANameHasLessThanTwoWords()
        {
            var detector = OwnTransferDetector.For(["Rafa", null, "", Holder]);

            Assert.True(detector.IsOwnTransfer("Pix enviado: \"Cp :18236120-Fulano Beltrano Ciclano\""));
            Assert.False(detector.IsOwnTransfer("Pix enviado: \"Cp :18236120-RAFA\""));
        }

        [Fact]
        public void IsOwnTransfer_ShouldMatchEachHolderWithItsOwnRule()
        {
            // "Fulano Ciclano" (2 words) must be together; the 3-word holder may have names in between.
            var detector = OwnTransferDetector.For(["Fulano Ciclano", "Rafael Moreira Ferreira"]);

            Assert.False(detector.IsOwnTransfer("PIX ENVIADO FULANO BELTRANO CICLANO"));
            Assert.True(detector.IsOwnTransfer("PIX ENVIADO RAFAEL SOUZA MOREIRA FERREIRA"));
        }

        [Fact]
        public void IsOwnTransfer_ShouldFindNothing_WhenThereAreNoHolders()
        {
            Assert.False(OwnTransferDetector.For([]).IsOwnTransfer("Pix enviado: \"Cp :18236120-Fulano Beltrano Ciclano\""));
        }

        [Theory]
        [InlineData("Rafael Moreira Ferreira", 3)]
        [InlineData("Ana de Sá", 1)]
        [InlineData("José da Silva dos Santos", 3)]
        [InlineData("Jo da Silva", 1)]
        [InlineData("Empresa X Ltda", 2)]
        [InlineData("Maria-Clara Souza", 3)]
        [InlineData("  ", 0)]
        [InlineData(null, 0)]
        public void CountNameWords_ShouldCountOnlyTheWordsThatCanBePartOfAName(string? name, int expected)
        {
            Assert.Equal(expected, OwnTransferDetector.CountNameWords(name));
        }
    }
}
