using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests.ScreenSpaceHiDpiIntegration.Editor.Tests
{
    public sealed class ScreenSpaceHiDpiPackageIdentityTests
    {
        private const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string GuidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void ExactTwoHostFolderMetasAreRecordedButExcludedFromEquivalentPayload()
        {
            var source = new[] { Entry("Resources/Milestro/icudtl.dat.bytes", ShaA) };
            var loaded = new[] { Entry("Resources/Milestro/icudtl.dat.bytes", ShaA) };

            ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentPayload(source, loaded);
            var metadata = ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths,
                new[]
                {
                    Host(ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath, GuidA),
                    Host(ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath, GuidB)
                });

            Assert.That(metadata, Has.Length.EqualTo(2));
            Assert.That(metadata[0].path,
                Is.EqualTo(ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath));
            Assert.That(metadata[1].path,
                Is.EqualTo(ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath));
        }

        [Test]
        public void NonAllowlistedPayloadMetaFailsClosed()
        {
            var source = new[] { Entry("Resources/Milestro/icudtl.dat.bytes", ShaA) };
            var loaded = new[]
            {
                Entry("Resources/Milestro/icudtl.dat.bytes", ShaA),
                Entry("Resources/Unexpected.meta", ShaB, GuidA)
            };

            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentPayload(source, loaded));
        }

        [Test]
        public void SourceContainingEitherHostMetaFailsClosed()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                    new[] { ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath },
                    ExactHostMetadata()));
        }

        [Test]
        public void MissingOneLoadedHostMetaFailsClosed()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                    ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths,
                    new[] { Host(ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath, GuidA) }));
        }

        [Test]
        public void ThirdLoadedHostMetaFailsClosed()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                    ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths,
                    new[]
                    {
                        Host(ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath, GuidA),
                        Host(ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath, GuidB),
                        Host("Resources/Unexpected.meta", "cccccccccccccccccccccccccccccccc")
                    }));
        }

        [Test]
        public void NonFolderMetaContentFailsClosed()
        {
            var metadata = ExactHostMetadata();
            metadata[1].folderAsset = false;
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactHostContainerMetadata(
                    ScreenSpaceHiDpiPackageManifest.HostContainerMetaPaths,
                    metadata));
        }

        [Test]
        public void LoadedMetaClosureAllowsOnlyExactTwoHostPaths()
        {
            var source = new[] { "Milestro.meta" };
            var exactLoaded = new[]
            {
                "Milestro.meta",
                ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath,
                ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath
            };
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactLoadedMetaClosure(source, exactLoaded);

            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireExactLoadedMetaClosure(source,
                    exactLoaded.Concat(new[] { "Resources/Unexpected.meta" }).ToArray()));
        }

        [Test]
        public void LoadedDirectoryLayoutRejectsAnyExtraDirectory()
        {
            var source = new[] { "Resources", "Resources/Milestro" };
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentDirectoryLayout(source, source);
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentDirectoryLayout(source,
                    source.Concat(new[] { "Resources/Unexpected" }).ToArray()));
        }

        [Test]
        public void LoadedMetaGuidsMustBeGloballyUnique()
        {
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniqueMetaGuids(new[] { GuidA, GuidB });
            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniqueMetaGuids(new[] { GuidA, GuidA }));
        }

        [Test]
        public void WrongIcuPayloadHashFailsClosed()
        {
            var source = new[] { Entry(ScreenSpaceHiDpiFixtureBuilder.IcuPayloadPath, ShaA) };
            var loaded = new[] { Entry(ScreenSpaceHiDpiFixtureBuilder.IcuPayloadPath, ShaB) };

            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireEquivalentPayload(source, loaded));
        }

        [Test]
        public void RuntimeIcuValidatorAcceptsExactlyOneMatchingResource()
        {
            var resource = new TextAsset("exact ICU payload");
            try
            {
                var expected = ScreenSpaceHiDpiHash.Sha256(resource.bytes);
                Assert.That(ScreenSpaceHiDpiIcuPayloadValidator.Validate(resource,
                        new[] { resource },
                        expected),
                    Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        [Test]
        public void RuntimeIcuValidatorRejectsWrongBytes()
        {
            var resource = new TextAsset("wrong ICU payload");
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ScreenSpaceHiDpiIcuPayloadValidator.Validate(resource,
                        new[] { resource },
                        ShaA));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        [Test]
        public void RuntimeIcuValidatorRejectsDuplicateResources()
        {
            var resource = new TextAsset("exact ICU payload");
            var duplicate = new TextAsset("exact ICU payload");
            try
            {
                var expected = ScreenSpaceHiDpiHash.Sha256(resource.bytes);
                Assert.Throws<InvalidOperationException>(() =>
                    ScreenSpaceHiDpiIcuPayloadValidator.Validate(resource,
                        new[] { resource, duplicate },
                        expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(resource);
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void PhysicalIcuMustBeUniqueAndInsideLoadedRoot()
        {
            const string expected = "/host/Assets/Plugins/Milestro/Resources/Milestro/icudtl.dat.bytes";
            ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniquePhysicalIcuResource(expected,
                new[] { expected });

            Assert.Throws<InvalidOperationException>(() =>
                ScreenSpaceHiDpiPackageIdentityPolicy.RequireUniquePhysicalIcuResource(expected,
                    new[]
                    {
                        expected,
                        "/host/Assets/Resources/Milestro/icudtl.dat.bytes"
                    }));
        }

        private static ScreenSpaceHiDpiHostContainerMetadata[] ExactHostMetadata()
        {
            return new[]
            {
                Host(ScreenSpaceHiDpiPackageManifest.RootHostContainerMetaPath, GuidA),
                Host(ScreenSpaceHiDpiPackageManifest.NestedHostContainerMetaPath, GuidB)
            };
        }

        private static ScreenSpaceHiDpiHostContainerMetadata Host(string path, string guid)
        {
            return new ScreenSpaceHiDpiHostContainerMetadata
            {
                path = path,
                sha256 = ShaB,
                guid = guid,
                sourcePathAbsent = true,
                folderAsset = true
            };
        }

        private static ScreenSpaceHiDpiPackageManifestEntry Entry(string path,
            string sha256,
            string guid = "")
        {
            return new ScreenSpaceHiDpiPackageManifestEntry
            {
                path = path,
                sha256 = sha256,
                guid = guid
            };
        }
    }
}
