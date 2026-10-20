using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Domain.Test.StatementImports
{
    public class InvestmentDetectorTests
    {
        [Theory]
        [InlineData("Aplicacao: \"CDB PORQUINHO BANCO INTER S A\"")]
        [InlineData("Aplicacao: \"CDB POS DI LIQ. BANCO INTER SA\"")]
        [InlineData("Aplicação RDB")]
        [InlineData("Dinheiro guardado com resgate planejado")]
        [InlineData("APLIC CDB 123")]
        [InlineData("Compra Tesouro Direto: \"TESOURO SELIC 2029\"")]
        [InlineData("Debito Tesouro Direto")]
        public void Detect_ShouldFindAnApplication_WhenMoneyGoesOutToAnInvestment(string description)
        {
            Assert.Equal(InvestmentMovement.Application, InvestmentDetector.Detect(description, -500m));
        }

        [Theory]
        [InlineData("Resgate: \"CDB POS DI LIQ. BANCO INTER S A\"")]
        [InlineData("Resgate: \"CDB PORQUINHO BANCO INTER S A\"")]
        [InlineData("Resgate RDB")]
        [InlineData("Credito Tesouro Direto: \"* PROV * VENDA TD NTN-B1\"")]
        [InlineData("Dinheiro resgatado")]
        [InlineData("Venda Tesouro Direto")]
        [InlineData("Estorno aplicacao CDB")]
        public void Detect_ShouldFindARedemption_WhenMoneyComesBackFromAnInvestment(string description)
        {
            Assert.Equal(InvestmentMovement.Redemption, InvestmentDetector.Detect(description, 610m));
        }

        [Fact]
        public void Detect_ShouldFindARedemption_WhenAnApplicationIsRefunded()
        {
            Assert.Equal(
                InvestmentMovement.Redemption,
                InvestmentDetector.Detect("Aplicação - Estorno: \"CDB PORQUINHO FULANO CICLANO\"", 50m));
        }

        [Fact]
        public void Detect_ShouldFindAnApplication_WhenMoneyGoesOutEvenIfTheLineMentionsARedemption()
        {
            Assert.Equal(
                InvestmentMovement.Application,
                InvestmentDetector.Detect("Dinheiro guardado com resgate planejado", -500m));
        }

        [Theory]
        [InlineData("Resgate RDB")]
        [InlineData("Credito Tesouro Direto: \"* PROV * VENDA TD NTN-B1\"")]
        public void Detect_ShouldFindNothing_WhenARedemptionLineTakesMoneyOut(string description)
        {
            Assert.Null(InvestmentDetector.Detect(description, -96m));
        }

        [Theory]
        [InlineData("Compra no debito: \"No estabelecimento EmporioCasaDe GOIANIA BRA\"", -70.58)]
        [InlineData("Pix enviado: \"Cp :00000000-Fulano de Tal\"", -40)]
        [InlineData("Pix recebido: \"Cp :00000000-FULANO DE TAL\"", 3167.36)]
        [InlineData("Pagamento efetuado: \"Debito Automatico Fatura Cartao Inter\"", -331.60)]
        [InlineData("Pagamento de fatura", -331.60)]
        [InlineData("Transferência recebida pelo Pix - FULANO DE TAL - •••.000.000-•• - BANCO INTER (0077)", 1000)]
        [InlineData("Pix enviado: \"Aplicacao financeira\"", -100)]
        [InlineData("Aplicativo de transporte", -25)]
        [InlineData("Resgatei o troco", 10)]
        public void Detect_ShouldFindNothing_WhenTheLineIsNotAnInvestment(string description, double amount)
        {
            Assert.Null(InvestmentDetector.Detect(description, (decimal)amount));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Detect_ShouldFindNothing_WhenThereIsNoDescription(string? description)
        {
            Assert.Null(InvestmentDetector.Detect(description, -100m));
        }
    }
}
