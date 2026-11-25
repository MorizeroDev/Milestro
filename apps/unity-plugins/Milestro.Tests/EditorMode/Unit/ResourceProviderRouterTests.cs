using System;
using Milestro.ResourceManagement;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ResourceProviderRouterTests
    {
        [TestCase("icons/coin")]
        [TestCase("/icons/coin")]
        [TestCase("C:icons/coin")]
        [TestCase("C:/Images/coin.png")]
        [TestCase(@"C:\Images\coin.png")]
        [TestCase(@"\\server\share\coin.png")]
        [TestCase("//server/share/coin.png")]
        [TestCase("file:coin.png")]
        [TestCase("icons/file://coin")]
        [TestCase(" file://coin ")]
        [TestCase("1file://coin")]
        [TestCase("my_scheme://coin")]
        [TestCase("图片://coin")]
        [TestCase("://coin")]
        [TestCase("")]
        public void InputsWithoutExplicitSchemeUseDefaultUnchanged(string source)
        {
            var received = "unset";
            Action<string> fallback = value => received = value;
            var router = new ResourceProviderRouter<Action<string>>(fallback);
            router.Register("file", value => Assert.Fail("Unexpected file provider invocation."));

            router.Resolve(source)(source);

            Assert.That(received, Is.EqualTo(source));
        }

        [TestCase("file:///Images/coin%20icon.png?version=1#part")]
        [TestCase("FILE:///Images/coin.png")]
        [TestCase("file://")]
        public void ExplicitSchemeSelectsProviderWithoutChangingSource(string source)
        {
            var received = "unset";
            Action<string> provider = value => received = value;
            var router = new ResourceProviderRouter<Action<string>>(
                value => Assert.Fail("Unexpected default provider invocation."));
            router.Register("file", provider);

            router.Resolve(source)(source);

            Assert.That(received, Is.EqualTo(source));
        }

        [Test]
        public void OneProviderCanRegisterMultipleSchemes()
        {
            var fallback = new object();
            var provider = new object();
            var router = new ResourceProviderRouter<object>(fallback);
            router.Register("asset+v2", provider);
            router.Register("asset.cache-v1", provider);

            Assert.That(router.Resolve("ASSET+V2://coin"), Is.SameAs(provider));
            Assert.That(router.Resolve("asset.cache-v1://coin"), Is.SameAs(provider));
        }

        [Test]
        public void UnknownExplicitSchemeDoesNotFallBack()
        {
            var router = new ResourceProviderRouter<object>(new object());

            Assert.Throws<InvalidOperationException>(() => router.Resolve("missing://coin"));
        }

        [Test]
        public void DuplicateRegistrationFailsWithoutReplacingProvider()
        {
            var original = new object();
            var router = new ResourceProviderRouter<object>(new object());
            router.Register("file", original);

            Assert.Throws<InvalidOperationException>(() => router.Register("FILE", new object()));
            Assert.That(router.Resolve("file://coin"), Is.SameAs(original));
        }

        [Test]
        public void ReplacementAndUnregistrationDoNotChangePreviouslyResolvedInstance()
        {
            var original = new object();
            var replacement = new object();
            var router = new ResourceProviderRouter<object>(new object());
            router.Register("file", original);
            var resolved = router.Resolve("file://coin");

            router.Replace("FILE", replacement);

            Assert.That(router.Resolve("file://coin"), Is.SameAs(replacement));
            Assert.That(resolved, Is.SameAs(original));
            Assert.That(router.Unregister("FILE"), Is.True);
            Assert.That(router.Unregister("file"), Is.False);
            Assert.Throws<InvalidOperationException>(() => router.Resolve("file://coin"));
            Assert.That(resolved, Is.SameAs(original));
        }

        [Test]
        public void ReplacementRequiresExistingRegistration()
        {
            var router = new ResourceProviderRouter<object>(new object());

            Assert.Throws<InvalidOperationException>(() => router.Replace("file", new object()));
        }

        [Test]
        public void DefaultCanBeReplacedWithoutChangingExplicitRoutes()
        {
            var provider = new object();
            var replacement = new object();
            var router = new ResourceProviderRouter<object>(new object());
            router.Register("file", provider);

            router.DefaultProvider = replacement;

            Assert.That(router.Resolve("/coin"), Is.SameAs(replacement));
            Assert.That(router.Resolve("file://coin"), Is.SameAs(provider));
        }

        [TestCase("")]
        [TestCase("file://")]
        [TestCase(" file")]
        [TestCase("file ")]
        [TestCase("1file")]
        [TestCase("my_scheme")]
        [TestCase("图片")]
        public void InvalidRegistrationSchemesAreRejected(string scheme)
        {
            var router = new ResourceProviderRouter<object>(new object());

            Assert.Throws<ArgumentException>(() => router.Register(scheme, new object()));
            Assert.Throws<ArgumentException>(() => router.Replace(scheme, new object()));
            Assert.Throws<ArgumentException>(() => router.Unregister(scheme));
        }

        [Test]
        public void NullArgumentsAreRejectedWithoutChangingRegistrations()
        {
            var original = new object();
            var router = new ResourceProviderRouter<object>(original);
            router.Register("file", original);

            Assert.Throws<ArgumentNullException>(() => new ResourceProviderRouter<object>(null));
            Assert.Throws<ArgumentNullException>(() => router.DefaultProvider = null);
            Assert.Throws<ArgumentNullException>(() => router.Register(null, original));
            Assert.Throws<ArgumentNullException>(() => router.Register("other", null));
            Assert.Throws<ArgumentNullException>(() => router.Replace("file", null));
            Assert.Throws<ArgumentNullException>(() => router.Resolve(null));
            Assert.That(router.DefaultProvider, Is.SameAs(original));
            Assert.That(router.Resolve("file://coin"), Is.SameAs(original));
        }
    }
}
