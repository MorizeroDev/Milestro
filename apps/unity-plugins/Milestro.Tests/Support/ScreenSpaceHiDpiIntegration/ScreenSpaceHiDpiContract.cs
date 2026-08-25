using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace Milestro.Tests.ScreenSpaceHiDpiIntegration
{
    [Serializable]
    [Preserve]
    public sealed class ScreenSpaceHiDpiPackageManifest
    {
        public const string SchemaValue = "screen-space-hidpi-package-manifest-v2";
        public const string RootHostContainerMetaPath = "Resources.meta";
        public const string NestedHostContainerMetaPath = "Resources/Milestro.meta";
        public const string ExpectedIcuSha256 =
            "310d9b2cb42947fad7f388b09d2ae574e2adeb60ca76abf280438908f20b2d7b";
        public const int ExpectedFileCount = 424;
        public const int ExpectedGuidCount = 232;

        public static string[] HostContainerMetaPaths => new[]
        {
            RootHostContainerMetaPath,
            NestedHostContainerMetaPath
        };

        public string schema = SchemaValue;
        public string exactHead = string.Empty;
        public string exactTree = string.Empty;
        public string contentSha256 = string.Empty;
        public int fileCount;
        public int guidCount;
        public ScreenSpaceHiDpiPackageManifestEntry[] entries = Array.Empty<ScreenSpaceHiDpiPackageManifestEntry>();
        public ScreenSpaceHiDpiHostContainerMetadata[] hostContainerMetadata =
            Array.Empty<ScreenSpaceHiDpiHostContainerMetadata>();

        public string ComputeContentSha256()
        {
            var builder = new StringBuilder();
            foreach (var entry in entries.OrderBy(value => value.path, StringComparer.Ordinal))
            {
                builder.Append(entry.path)
                    .Append('\t')
                    .Append(entry.sha256)
                    .Append('\t')
                    .Append(entry.guid)
                    .Append('\n');
            }

            return ScreenSpaceHiDpiHash.Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        public void Validate(string expectedHead, string expectedTree, string expectedContentSha256)
        {
            if (!string.Equals(schema, SchemaValue, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Embedded package manifest schema is invalid.");
            }
            RequireObjectId(exactHead, nameof(exactHead));
            RequireObjectId(exactTree, nameof(exactTree));
            if (!string.Equals(exactHead, expectedHead, StringComparison.Ordinal) ||
                !string.Equals(exactTree, expectedTree, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Embedded package manifest Git identity does not match the build identity.");
            }

            if (entries == null || fileCount != ExpectedFileCount ||
                fileCount != entries.Length || guidCount != ExpectedGuidCount)
            {
                throw new InvalidOperationException("Embedded package manifest file count is invalid.");
            }

            var expectedHostPaths = HostContainerMetaPaths;
            if (hostContainerMetadata == null ||
                hostContainerMetadata.Length != expectedHostPaths.Length)
            {
                throw new InvalidOperationException(
                    "Embedded package manifest host-owned folder meta count is invalid.");
            }
            var hostMetadataByPath = hostContainerMetadata
                .GroupBy(metadata => metadata?.path ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            if (hostMetadataByPath.Count != expectedHostPaths.Length ||
                expectedHostPaths.Any(path => !hostMetadataByPath.TryGetValue(path, out var values) ||
                                              values.Length != 1))
            {
                throw new InvalidOperationException(
                    "Embedded package manifest host-owned folder meta paths are invalid.");
            }
            foreach (var path in expectedHostPaths)
            {
                var metadata = hostMetadataByPath[path][0];
                if (metadata == null || metadata.guid == null || metadata.guid.Length != 32 ||
                    metadata.guid.Any(character => !Uri.IsHexDigit(character)) ||
                    !ScreenSpaceHiDpiHash.IsSha256(metadata.sha256) ||
                    !metadata.sourcePathAbsent || !metadata.folderAsset)
                {
                    throw new InvalidOperationException(
                        "Embedded package manifest host-owned folder meta identity is invalid: " +
                        path + ".");
                }
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.path) ||
                    !paths.Add(entry.path) || !ScreenSpaceHiDpiHash.IsSha256(entry.sha256))
                {
                    throw new InvalidOperationException("Embedded package manifest contains an invalid or duplicate file entry.");
                }
                if (IsHostContainerMetaPath(entry.path))
                {
                    throw new InvalidOperationException(
                        "Host-owned folder metadata must not be counted as package payload.");
                }

                if (!string.IsNullOrEmpty(entry.guid) && !guids.Add(entry.guid))
                {
                    throw new InvalidOperationException("Embedded package manifest contains a duplicate Unity GUID.");
                }
            }

            if (guidCount != guids.Count)
            {
                throw new InvalidOperationException("Embedded package manifest GUID count is invalid.");
            }
            var hostGuids = hostContainerMetadata.Select(metadata => metadata.guid).ToArray();
            if (hostGuids.Distinct(StringComparer.Ordinal).Count() != hostGuids.Length ||
                hostGuids.Any(guids.Contains))
            {
                throw new InvalidOperationException(
                    "Host-owned folder meta GUID is duplicate or collides with package payload.");
            }

            var computed = ComputeContentSha256();
            if (!string.Equals(contentSha256, computed, StringComparison.Ordinal) ||
                !string.Equals(expectedContentSha256, computed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Embedded package manifest content hash does not match the clean build-time package.");
            }
        }

        public static void RequireObjectId(string value, string name)
        {
            if (value == null || value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException(name + " must be an exact 40-character Git object ID.", name);
            }
        }

        public static bool IsHostContainerMetaPath(string path)
        {
            return string.Equals(path, RootHostContainerMetaPath, StringComparison.Ordinal) ||
                   string.Equals(path, NestedHostContainerMetaPath, StringComparison.Ordinal);
        }
    }

    [Serializable]
    [Preserve]
    public sealed class ScreenSpaceHiDpiPackageManifestEntry
    {
        public string path = string.Empty;
        public string sha256 = string.Empty;
        public string guid = string.Empty;
    }

    [Serializable]
    [Preserve]
    public sealed class ScreenSpaceHiDpiHostContainerMetadata
    {
        public string path = string.Empty;
        public string sha256 = string.Empty;
        public string guid = string.Empty;
        public bool sourcePathAbsent;
        public bool folderAsset;
    }

    [Serializable]
    [Preserve]
    internal sealed class ScreenSpaceHiDpiLogRecord
    {
        public string schema = "screen-space-hidpi-log-v1";
        public string scenarioId = string.Empty;
        public string timestampUtc = string.Empty;
        public string phase = string.Empty;
        public bool terminal;
        public string automationStatus = string.Empty;
        public string humanVisualStatus = string.Empty;
        public string overallStatus = string.Empty;
        public string deviceEpochStatus = string.Empty;
        public string exactHead = string.Empty;
        public string exactTree = string.Empty;
        public string packageContentSha256 = string.Empty;
        public string packageManifestSha256 = string.Empty;
        public int packageFileCount;
        public int packageGuidCount;
        public ScreenSpaceHiDpiHostContainerMetadata[] hostContainerMetadata =
            Array.Empty<ScreenSpaceHiDpiHostContainerMetadata>();
        public string icuResourceSha256 = string.Empty;
        public string backend = string.Empty;
        public string graphicsDeviceType = string.Empty;
        public string graphicsDeviceName = string.Empty;
        public int displayCount;
        public int targetDisplay;
        public int displaySystemWidth;
        public int displaySystemHeight;
        public int displayRenderWidth;
        public int displayRenderHeight;
        public int screenWidth;
        public int screenHeight;
        public float screenDpi;
        public string component = string.Empty;
        public float requestedScale;
        public float clampedScale;
        public float effectiveScale;
        public int logicalWidth;
        public int logicalHeight;
        public int rasterWidth;
        public int rasterHeight;
        public int runtimeMaxTextureEdge;
        public int backendMaxTextureEdge;
        public int configuredMaxTextureEdge;
        public long maxPixelsPerSurface;
        public long maxBytesPerSurface;
        public long maxGlobalBytes;
        public long maxTransitionBytes;
        public long allocationAttempts;
        public long allocationSuccesses;
        public long allocationFailures;
        public long validationFailures;
        public long suppressedAttempts;
        public long atomicSwaps;
        public long retirements;
        public bool hasCounterDelta;
        public long allocationAttemptDelta;
        public long atomicSwapDelta;
        public long retirementDelta;
        public long committedBytes;
        public long reservedBytes;
        public long ledgerGeneration;
        public ulong deviceEpoch;
        public bool linkPhysicalPointer;
        public bool linkPayloadValid;
        public bool imeCompositionObserved;
        public bool imeCommitValid;
        public bool logicalStateStable;
        public bool worldSpaceExact;
        public bool lastKnownGoodPreserved;
        public bool recoverySucceeded;
        public string screenshotPath = string.Empty;
        public string screenshotSha256 = string.Empty;
        public string detail = string.Empty;
        public string error = string.Empty;
    }

    internal sealed class ScreenSpaceHiDpiRecorder : IDisposable
    {
        private readonly StreamWriter writer;
        private bool terminalWritten;
        private bool disposed;

        internal ScreenSpaceHiDpiRecorder(string exactHead,
            string exactTree,
            string backend,
            string expectedPackageContentSha256,
            TextAsset embeddedManifest)
        {
            ScreenSpaceHiDpiPackageManifest.RequireObjectId(exactHead, nameof(exactHead));
            ScreenSpaceHiDpiPackageManifest.RequireObjectId(exactTree, nameof(exactTree));
            if (string.IsNullOrEmpty(backend) || embeddedManifest == null ||
                !ScreenSpaceHiDpiHash.IsSha256(expectedPackageContentSha256))
            {
                throw new InvalidOperationException("Screen Space HiDPI build identity is incomplete.");
            }

            ExactHead = exactHead;
            ExactTree = exactTree;
            Backend = backend;
            ScenarioId = Guid.NewGuid().ToString("N");
            PackageManifestSha256 = ScreenSpaceHiDpiHash.Sha256(Encoding.UTF8.GetBytes(embeddedManifest.text));
            PackageManifest = JsonUtility.FromJson<ScreenSpaceHiDpiPackageManifest>(embeddedManifest.text) ??
                              throw new InvalidOperationException("Embedded package manifest JSON is invalid.");
            PackageManifest.Validate(exactHead, exactTree, expectedPackageContentSha256);

            LogPath = Path.Combine(Path.GetTempPath(),
                "screen-space-hidpi." + exactHead + "." + backend + ".jsonl");
            writer = new StreamWriter(new FileStream(LogPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read), new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }

        internal string ScenarioId { get; }
        internal string ExactHead { get; }
        internal string ExactTree { get; }
        internal string Backend { get; }
        internal string LogPath { get; }
        internal string PackageManifestSha256 { get; }
        internal ScreenSpaceHiDpiPackageManifest PackageManifest { get; }
        internal bool TerminalWritten => terminalWritten;

        internal void Write(ScreenSpaceHiDpiLogRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ScreenSpaceHiDpiRecorder));
            }
            if (terminalWritten)
            {
                throw new InvalidOperationException("Screen Space HiDPI recorder already wrote its terminal record.");
            }

            record.scenarioId = ScenarioId;
            record.timestampUtc = DateTime.UtcNow.ToString("O");
            record.exactHead = ExactHead;
            record.exactTree = ExactTree;
            record.packageContentSha256 = PackageManifest.contentSha256;
            record.packageManifestSha256 = PackageManifestSha256;
            record.packageFileCount = PackageManifest.fileCount;
            record.packageGuidCount = PackageManifest.guidCount;
            record.hostContainerMetadata = PackageManifest.hostContainerMetadata;
            record.backend = Backend;
            writer.WriteLine(JsonUtility.ToJson(record));
            Debug.Log("SCREEN_SPACE_HIDPI_HIDPI_RECORD " + JsonUtility.ToJson(record));
            terminalWritten = record.terminal;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            if (!terminalWritten)
            {
                Write(new ScreenSpaceHiDpiLogRecord
                {
                    phase = "terminal",
                    terminal = true,
                    automationStatus = "AUTOMATION_FAIL",
                    humanVisualStatus = "HUMAN_VISUAL_PENDING",
                    overallStatus = "INCOMPLETE",
                    deviceEpochStatus = "DEVICE_EPOCH_NOT_EXERCISED",
                    detail = "Recorder disposed before the scenario wrote a terminal record."
                });
            }
            disposed = true;
            writer.Dispose();
        }
    }

    public static class ScreenSpaceHiDpiHash
    {
        public static string Sha256File(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(stream));
        }

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(bytes));
        }

        public static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character => Uri.IsHexDigit(character));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    internal static class ScreenSpaceHiDpiIcuPayloadValidator
    {
        internal static string Validate(TextAsset? loadedResource,
            TextAsset[] matchingResources,
            string expectedSha256)
        {
            if (!ScreenSpaceHiDpiHash.IsSha256(expectedSha256))
            {
                throw new InvalidOperationException("Screen Space HiDPI expected ICU SHA-256 is invalid.");
            }
            if (loadedResource == null)
            {
                throw new InvalidOperationException(
                    "Resources.Load<TextAsset>(\"Milestro/icudtl.dat\") returned null.");
            }
            if (matchingResources == null || matchingResources.Length != 1 ||
                !ReferenceEquals(loadedResource, matchingResources[0]))
            {
                throw new InvalidOperationException(
                    "Screen Space HiDPI requires exactly one loaded Milestro/icudtl.dat TextAsset.");
            }

            var actualSha256 = ScreenSpaceHiDpiHash.Sha256(loadedResource.bytes);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Loaded Milestro/icudtl.dat bytes do not match the exact source payload.");
            }
            return actualSha256;
        }
    }
}
