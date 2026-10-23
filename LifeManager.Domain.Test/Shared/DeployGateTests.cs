namespace LifeManager.Domain.Test.Shared
{
    // Fails on purpose to check that a failing test stops the automatic deploy. Never merge this.
    public class DeployGateTests
    {
        [Fact]
        public void DeployGate_ShouldBlockTheDeploy_WhenATestFails()
        {
            Assert.Equal(1, 2);
        }
    }
}
