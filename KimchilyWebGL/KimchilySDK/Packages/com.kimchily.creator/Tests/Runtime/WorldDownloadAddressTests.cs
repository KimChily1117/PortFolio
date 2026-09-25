using System.IO;
using NUnit.Framework;
using Kimchily.Creator.Content;

namespace Kimchily.Creator.Tests
{
    public sealed class WorldDownloadAddressTests
    {
        const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public void AddressBindsTheRevisionAndRequiresHashAndReleaseTls()
        {
            Assert.AreEqual("example.com", WorldContentDownload.ValidateAddress(
                "https://example.com/worlds/sample/r1/world.json", Hash, "sample", "r1", false).Host);
            Assert.DoesNotThrow(() => WorldContentDownload.ValidateAddress(
                "http://127.0.0.1:8787/worlds/sample/r1/world.json", Hash, "sample", "r1", true));
            foreach (string url in new[] {
                "http://example.com/worlds/sample/r1/world.json", "file:///worlds/sample/r1/world.json",
                "https://user:pass@example.com/worlds/sample/r1/world.json", "https://example.com/worlds/sample/r2/world.json",
                "https://example.com/worlds/sample/r1/world.json?extra=1", "https://example.com/worlds/sample/r1/world.json#ignored" })
                Assert.Throws<InvalidDataException>(() => WorldContentDownload.ValidateAddress(url, Hash, "sample", "r1", false), url);
            Assert.Throws<InvalidDataException>(() => WorldContentDownload.ValidateAddress(
                "https://example.com/worlds/sample/r1/world.json", "bad", "sample", "r1", false));
            Assert.IsFalse(WorldContentDownload.IsIdentifier("../world"));
        }
    }
}
