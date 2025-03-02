﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Testing;
using Azure.Identity;
using Azure.Storage.Common;
using Azure.Storage.Common.Test;
using Azure.Storage.Sas;
using NUnit.Framework;
using TestConstants = Azure.Storage.Test.Constants;

namespace Azure.Storage.Test.Shared
{
    public abstract class StorageTestBase : RecordedTestBase
    {
        public StorageTestBase(bool async, RecordedTestMode? mode = null)
            : base(async, mode ?? RecordedTestUtilities.GetModeFromEnvironment())
        {
            Sanitizer = new StorageRecordedTestSanitizer();
            Matcher = new StorageRecordMatcher(Sanitizer);
        }

        /// <summary>
        /// Gets the tenant to use by default for our tests.
        /// </summary>
        public TenantConfiguration TestConfigDefault => GetTestConfig(
                "Storage_TestConfigDefault",
                () => TestConfigurations.DefaultTargetTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require Read Access
        /// Geo-Redundant Storage to be setup.
        /// </summary>
        public TenantConfiguration TestConfigSecondary => GetTestConfig(
                "Storage_TestConfigSecondary",
                () => TestConfigurations.DefaultSecondaryTargetTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require Premium SSDs.
        /// </summary>
        public TenantConfiguration TestConfigPremiumBlob => GetTestConfig(
                "Storage_TestConfigPremiumBlob",
                () => TestConfigurations.DefaultTargetPremiumBlobTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require preview features.
        /// </summary>
        public TenantConfiguration TestConfigPreviewBlob => GetTestConfig(
                "Storage_TestConfigPreviewBlob",
                () => TestConfigurations.DefaultTargetPreviewBlobTenant);

        /// <summary>
        /// Gets the tenant to use for any tests that require authentication
        /// with Azure AD.
        /// </summary>
        public TenantConfiguration TestConfigOAuth => GetTestConfig(
                "Storage_TestConfigOAuth",
                () => TestConfigurations.DefaultTargetOAuthTenant);

        /// <summary>
        /// Gets a cache used for storing serialized tenant configurations.  Do
        /// not get values from this directly; use GetTestConfig.
        /// </summary>
        private readonly Dictionary<string, string> _recordingConfigCache =
            new Dictionary<string, string>();

        /// <summary>
        /// Gets a cache used for storing deserialized tenant configurations.
        /// Do not get values from this directly; use GetTestConfig.
        private readonly Dictionary<string, TenantConfiguration> _playbackConfigCache =
            new Dictionary<string, TenantConfiguration>();

        /// <summary>
        /// We need to clear the playback cache before every test because
        /// different recordings might have used different tenant
        /// configurations.
        /// </summary>
        [SetUp]
        public virtual void ClearCaches() =>
            _playbackConfigCache.Clear();

        /// <summary>
        /// Get or create a test configuration tenant to use with our tests.
        ///
        /// If we're recording, we'll save a sanitized version of the test
        /// configuarion.  If we're playing recorded tests, we'll use the
        /// serialized test configuration.  If we're running the tests live,
        /// we'll just return the value.
        ///
        /// While we cache things internally, DO NOT cache them elsewhere
        /// because we need each test to have its configuration recorded.
        /// </summary>
        /// <param name="name">The name of the session record variable.</param>
        /// <param name="getTenant">
        /// A function to get the tenant.  This is wrapped in a Func becuase
        /// we'll throw Assert.Inconclusive if you try to access a tenant with
        /// an invalid config file.
        /// </param>
        /// <returns>A test tenant to use with our tests.</returns>
        private TenantConfiguration GetTestConfig(string name, Func<TenantConfiguration> getTenant)
        {
            TenantConfiguration config;
            string text;
            switch (Mode)
            {
                case RecordedTestMode.Playback:
                    if (!_playbackConfigCache.TryGetValue(name, out config))
                    {
                        text = Recording.GetVariable(name, null);
                        config = TenantConfiguration.Parse(text);
                        _playbackConfigCache[name] = config;
                    }
                    break;
                case RecordedTestMode.Record:
                    config = getTenant();
                    if (!_recordingConfigCache.TryGetValue(name, out text))
                    {
                        text = TenantConfiguration.Serialize(config, true);
                        _recordingConfigCache[name] = text;
                    }
                    Recording.GetVariable(name, text);
                    break;
                case RecordedTestMode.Live:
                default:
                    config = getTenant();
                    break;
            }
            return config;
        }

        public DateTimeOffset GetUtcNow() => Recording.UtcNow;

        public byte[] GetRandomBuffer(long size)
            => TestHelper.GetRandomBuffer(size, Recording.Random);

        public string GetNewString(int length = 20)
        {
            var buffer = new char[length];
            for (var i = 0; i < length; i++)
            {
                buffer[i] = (char)('a' + Recording.Random.Next(0, 25));
            }
            return new string(buffer);
        }

        public string GetNewMetadataName() => $"test_metadata_{Recording.Random.NewGuid().ToString().Replace("-", "_")}";

        public IDictionary<string, string> BuildMetadata()
            => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "foo", "bar" },
                    { "meta", "data" }
                };

        public IPAddress GetIPAddress()
        {
            var a = Recording.Random.Next(0, 256);
            var b = Recording.Random.Next(0, 256);
            var c = Recording.Random.Next(0, 256);
            var d = Recording.Random.Next(0, 256);
            var ipString = $"{a}.{b}.{c}.{d}";
            return IPAddress.Parse(ipString);
        }

        public TokenCredential GetOAuthCredential() =>
            GetOAuthCredential(TestConfigOAuth);

        public TokenCredential GetOAuthCredential(TenantConfiguration config) =>
            GetOAuthCredential(
                config.ActiveDirectoryTenantId,
                config.ActiveDirectoryApplicationId,
                config.ActiveDirectoryApplicationSecret,
                new Uri(config.ActiveDirectoryAuthEndpoint));

        public TokenCredential GetOAuthCredential(string tenantId, string appId, string secret, Uri authorityHost) =>
            new ClientSecretCredential(
                tenantId,
                appId,
                secret,
                Recording.InstrumentClientOptions(
                    new IdentityClientOptions() { AuthorityHost = authorityHost }));

        public void AssertMetadataEquality(IDictionary<string, string> expected, IDictionary<string, string> actual)
        {
            Assert.IsNotNull(expected, "Expected metadata is null");
            Assert.IsNotNull(actual, "Actual metadata is null");

            Assert.AreEqual(expected.Count, actual.Count, "Metadata counts are not equal");

            foreach (KeyValuePair<string, string> kvp in expected)
            {
                if (!actual.TryGetValue(kvp.Key, out var value) ||
                    string.Compare(kvp.Value, value, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    Assert.Fail($"Expected key <{kvp.Key}> with value <{kvp.Value}> not found");
                }
            }
        }

        /// <summary>
        /// To prevent test flakiness, we simply warn when certain timing sensitive
        /// tests don't appear to work as expected.  However, we will ask you to run
        /// it again if you're recording a test because it should work correctly at
        /// least then.
        /// </summary>
        public void WarnCopyCompletedTooQuickly()
        {
            if (Mode == RecordedTestMode.Record)
            {
                Assert.Fail("Copy may have completed too quickly to abort.  Please record again.");
            }
            else
            {
                Assert.Inconclusive("Copy may have completed too quickly to abort.");
            }
        }

        /// <summary>
        /// A number of our tests have built in delays while we wait an expected
        /// amount of time for a service operation to complete and this method
        /// allows us to wait (unless we're playing back recordings, which can
        /// complete immediately).
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to wait.</param>
        /// <param name="playbackDelayMilliseconds">
        /// An optional number of milliseconds to wait if we're playing back a
        /// recorded test.  This is useful for allowing client side events to
        /// get processed.
        /// </param>
        /// <returns>A task that will (optionally) delay.</returns>
        public async Task Delay(int milliseconds = 1000, int? playbackDelayMilliseconds = null)
        {
            if (Mode != RecordedTestMode.Playback)
            {
                await Task.Delay(milliseconds);
            }
            else if (playbackDelayMilliseconds != null)
            {
                await Task.Delay(playbackDelayMilliseconds.Value);
            }
        }

        /// <summary>
        /// Wait for the progress notifications to complete.
        /// </summary>
        /// <param name="progressList">
        /// The list of progress notifications being updated by the Progress handler.
        /// </param>
        /// <param name="totalSize">The total size we should eventually see.</param>
        /// <returns>A task that will (optionally) delay.</returns>
        protected async Task WaitForProgressAsync(List<StorageProgress> progressList, long totalSize)
        {
            for (var attempts = 0; attempts < 10; attempts++)
            {
                if (progressList.LastOrDefault()?.BytesTransferred >= totalSize)
                {
                    return;
                }

                // Wait for lingering progress events
                await Delay(500, 100).ConfigureAwait(false);
            }

            // TODO: #7077 - These are too flaky/noisy so I'm changing to Warn
            Assert.Warn("Progress notifications never completed!");
        }

        protected void AssertSecondaryStorageFirstRetrySuccessful(string primaryHost, string secondaryHost, TestExceptionPolicy testExceptionPolicy)
        {
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[0]);
            Assert.AreEqual(secondaryHost, testExceptionPolicy.HostsSetInRequests[1]);
        }

        protected void AssertSecondaryStorageSecondRetrySuccessful(string primaryHost, string secondaryHost, TestExceptionPolicy testExceptionPolicy)
        {
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[0]);
            Assert.AreEqual(secondaryHost, testExceptionPolicy.HostsSetInRequests[1]);
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[2]);
        }

        protected void AssertSecondaryStorageThirdRetrySuccessful(string primaryHost, string secondaryHost, TestExceptionPolicy testExceptionPolicy)
        {
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[0]);
            Assert.AreEqual(secondaryHost, testExceptionPolicy.HostsSetInRequests[1]);
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[2]);
            Assert.AreEqual(secondaryHost, testExceptionPolicy.HostsSetInRequests[3]);
        }

        protected void AssertSecondaryStorage404OnSecondary(string primaryHost, string secondaryHost, TestExceptionPolicy testExceptionPolicy)
        {
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[0]);
            Assert.AreEqual(secondaryHost, testExceptionPolicy.HostsSetInRequests[1]);
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[2]);
            Assert.AreEqual(primaryHost, testExceptionPolicy.HostsSetInRequests[3]);
        }

        protected async Task<T> EnsurePropagatedAsync<T>(
            Func<Task<T>> getResponse,
            Func<T,bool> hasResponse)
        {
            int delayDuration = 10000;
            bool responseReceived = false;
            T response = default;
            // end time of 16 minutes from now to allow for propagation to secondary host
            DateTimeOffset endTime = DateTimeOffset.Now.AddMinutes(16);
            while (!responseReceived && DateTimeOffset.Now < endTime)
            {
                response = await getResponse();
                if (!hasResponse(response))
                {
                    await this.Delay(delayDuration);
                }
                else
                {
                    responseReceived = true;
                }
            }
            return response;
        }

        internal void AssertResponseHeaders(TestConstants constants, SasQueryParameters sasQueryParameters)
        {
            Assert.AreEqual(constants.Sas.CacheControl, sasQueryParameters.CacheControl);
            Assert.AreEqual(constants.Sas.ContentDisposition, sasQueryParameters.ContentDisposition);
            Assert.AreEqual(constants.Sas.ContentEncoding, sasQueryParameters.ContentEncoding);
            Assert.AreEqual(constants.Sas.ContentLanguage, sasQueryParameters.ContentLanguage);
            Assert.AreEqual(constants.Sas.ContentType, sasQueryParameters.ContentType);
        }
    }
}
