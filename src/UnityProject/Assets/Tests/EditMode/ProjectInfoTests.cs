using NUnit.Framework;
using Pacifico.Core;

namespace Pacifico.Tests
{
    public class ProjectInfoTests
    {
        [TestCase(1879, true)]
        [TestCase(1884, true)]
        [TestCase(1878, false)]
        [TestCase(1885, false)]
        public void IsWithinWarPeriod_RespetaLimitesHistoricos(int year, bool expected)
        {
            Assert.AreEqual(expected, ProjectInfo.IsWithinWarPeriod(year));
        }

        [Test]
        public void Version_NoEstaVacia()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(ProjectInfo.Version));
        }
    }
}
