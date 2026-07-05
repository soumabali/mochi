using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MochiV2.Tests
{
    /// <summary>
    /// Placeholder test that verifies the T-001 bootstrap surface is present.
    /// Subsequent tasks will add domain-specific tests (animation, FSM, etc.).
    /// </summary>
    public class BootstrapTests
    {
        [Fact]
        public void Program_ResolveLogDirectory_ReturnsNekoCompanionLogsPath()
        {
            string logDir = Program.ResolveLogDirectory();

            Assert.Contains("NekoCompanion", logDir);
            Assert.EndsWith("logs", logDir);
        }

        [Fact]
        public void Program_ConfigureServices_DoesNotThrow()
        {
            // T-001: empty service collection — should build cleanly.
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Program.ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            Assert.NotNull(provider);
        }

        [Fact]
        public void ProjectScaffold_Placeholder_AlwaysPasses()
        {
            // Minimal placeholder so `dotnet test` has at least one passing
            // test. Removed once real tests arrive in later tasks.
            Assert.True(true);
        }
    }
}