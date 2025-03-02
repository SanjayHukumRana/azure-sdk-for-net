﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core.Testing;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Common.Test;
using Azure.Storage.Sas;

namespace Azure.Storage.Test.Shared
{
    public abstract class BlobTestBase : StorageTestBase
    {
        public readonly string ReceivedETag = "\"received\"";
        public readonly string GarbageETag = "\"garbage\"";
        public readonly string ReceivedLeaseId = "received";

        protected string SecondaryStorageTenantPrimaryHost() =>
            new Uri(TestConfigSecondary.BlobServiceEndpoint).Host;

        protected string SecondaryStorageTenantSecondaryHost() =>
            new Uri(TestConfigSecondary.BlobServiceSecondaryEndpoint).Host;

        public BlobTestBase(bool async) : this(async, null) { }

        public BlobTestBase(bool async, RecordedTestMode? mode = null)
            : base(async, mode)
        {
        }

        public DateTimeOffset OldDate => Recording.Now.AddDays(-1);
        public DateTimeOffset NewDate => Recording.Now.AddDays(1);

        public string GetGarbageLeaseId() => Recording.Random.NewGuid().ToString();
        public string GetNewContainerName() => $"test-container-{Recording.Random.NewGuid()}";
        public string GetNewBlobName() => $"test-blob-{Recording.Random.NewGuid()}";
        public string GetNewBlockName() => $"test-block-{Recording.Random.NewGuid()}";

        public BlobClientOptions GetOptions(bool parallelRange = false)
        {
            var options = new BlobClientOptions
            {
                Diagnostics = { IsLoggingEnabled = true },
                Retry =
                {
                    Mode = RetryMode.Exponential,
                    MaxRetries = Azure.Storage.Constants.MaxReliabilityRetries,
                    Delay = TimeSpan.FromSeconds(Mode == RecordedTestMode.Playback ? 0.01 : 0.5),
                    MaxDelay = TimeSpan.FromSeconds(Mode == RecordedTestMode.Playback ? 0.1 : 10)
                }
            };
            if (Mode != RecordedTestMode.Live)
            {
                options.AddPolicy(new RecordedClientRequestIdPolicy(Recording, parallelRange), HttpPipelinePosition.PerCall);
            }

            return Recording.InstrumentClientOptions(options);
        }

        public BlobClientOptions GetFaultyBlobConnectionOptions(
            int raiseAt = default,
            Exception raise = default)
        {
            raise = raise ?? new Exception("Simulated connection fault");
            BlobClientOptions options = GetOptions();
            options.AddPolicy(new FaultyDownloadPipelinePolicy(raiseAt, raise), HttpPipelinePosition.PerCall);
            return options;
        }

        private BlobServiceClient GetServiceClientFromSharedKeyConfig(TenantConfiguration config)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri(config.BlobServiceEndpoint),
                    new StorageSharedKeyCredential(config.AccountName, config.AccountKey),
                    GetOptions()));

        private BlobServiceClient GetSecondaryReadServiceClient(TenantConfiguration config, int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
        {
            BlobClientOptions options = GetSecondaryStorageOptions(config, out testExceptionPolicy, numberOfReadFailuresToSimulate, simulate404, enabledRequestMethods);
            return InstrumentClient(
                 new BlobServiceClient(
                    new Uri(config.BlobServiceEndpoint),
                    new StorageSharedKeyCredential(config.AccountName, config.AccountKey),
                    options));
        }

        private BlobBaseClient GetSecondaryReadBlobBaseClient(TenantConfiguration config, int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
        {
            BlobClientOptions options = GetSecondaryStorageOptions(config, out testExceptionPolicy, numberOfReadFailuresToSimulate, simulate404, enabledRequestMethods);
            return InstrumentClient(
                 new BlobBaseClient(
                    new Uri(config.BlobServiceEndpoint),
                    new StorageSharedKeyCredential(config.AccountName, config.AccountKey),
                    options));
        }

        private BlobContainerClient GetSecondaryReadBlobContainerClient(TenantConfiguration config, int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
        {
            BlobClientOptions options = GetSecondaryStorageOptions(config, out testExceptionPolicy, numberOfReadFailuresToSimulate, simulate404, enabledRequestMethods);
            Uri uri = new Uri(config.BlobServiceEndpoint);
            string containerName = GetNewContainerName();
            return InstrumentClient(
                 new BlobContainerClient(
                    uri.AppendToPath(containerName),
                    new StorageSharedKeyCredential(config.AccountName, config.AccountKey),
                    options));
        }

        private BlobClientOptions GetSecondaryStorageOptions(
            TenantConfiguration config,
            out TestExceptionPolicy testExceptionPolicy,
            int numberOfReadFailuresToSimulate = 1,
            bool simulate404 = false,
            List<RequestMethod> trackedRequestMethods = null)
        {
            BlobClientOptions options = GetOptions();
            options.GeoRedundantSecondaryUri = new Uri(config.BlobServiceSecondaryEndpoint);
            options.Retry.MaxRetries = 4;
            testExceptionPolicy = new TestExceptionPolicy(numberOfReadFailuresToSimulate, options.GeoRedundantSecondaryUri, simulate404, trackedRequestMethods);
            options.AddPolicy(testExceptionPolicy, HttpPipelinePosition.PerRetry);
            return options;
        }

        private BlobServiceClient GetServiceClientFromOauthConfig(TenantConfiguration config) =>
            InstrumentClient(
                new BlobServiceClient(
                    new Uri(config.BlobServiceEndpoint),
                    GetOAuthCredential(config),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_SharedKey()
            => GetServiceClientFromSharedKeyConfig(TestConfigDefault);

        public BlobServiceClient GetServiceClient_SecondaryAccount_ReadEnabledOnRetry(int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
            => GetSecondaryReadServiceClient(TestConfigSecondary, numberOfReadFailuresToSimulate, out testExceptionPolicy, simulate404, enabledRequestMethods);

        public BlobBaseClient GetBlobBaseClient_SecondaryAccount_ReadEnabledOnRetry(int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
    => GetSecondaryReadBlobBaseClient(TestConfigSecondary, numberOfReadFailuresToSimulate, out testExceptionPolicy, simulate404, enabledRequestMethods);

        public BlobContainerClient GetBlobContainerClient_SecondaryAccount_ReadEnabledOnRetry(int numberOfReadFailuresToSimulate, out TestExceptionPolicy testExceptionPolicy, bool simulate404 = false, List<RequestMethod> enabledRequestMethods = null)
    => GetSecondaryReadBlobContainerClient(TestConfigSecondary, numberOfReadFailuresToSimulate, out testExceptionPolicy, simulate404, enabledRequestMethods);

        public BlobServiceClient GetServiceClient_SecondaryAccount_SharedKey()
            => GetServiceClientFromSharedKeyConfig(TestConfigSecondary);

        public BlobServiceClient GetServiceClient_PreviewAccount_SharedKey()
            => GetServiceClientFromSharedKeyConfig(TestConfigPreviewBlob);

        public BlobServiceClient GetServiceClient_PremiumBlobAccount_SharedKey()
            => GetServiceClientFromSharedKeyConfig(TestConfigPremiumBlob);

        public BlobServiceClient GetServiceClient_OauthAccount() =>
            GetServiceClientFromOauthConfig(TestConfigOAuth);

        public BlobServiceClient GetServiceClient_AccountSas(
            StorageSharedKeyCredential sharedKeyCredentials = default,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigDefault.BlobServiceEndpoint}?{sasCredentials ?? GetNewAccountSasCredentials(sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_BlobServiceSas_Container(
            string containerName,
            StorageSharedKeyCredential sharedKeyCredentials = default,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigDefault.BlobServiceEndpoint}?{sasCredentials ?? GetNewBlobServiceSasCredentialsContainer(containerName: containerName, sharedKeyCredentials: sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_BlobServiceIdentitySas_Container(
            string containerName,
            UserDelegationKey userDelegationKey,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigOAuth.BlobServiceEndpoint}?{sasCredentials ?? GetNewBlobServiceIdentitySasCredentialsContainer(containerName: containerName, userDelegationKey, TestConfigOAuth.AccountName)}"),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_BlobServiceSas_Blob(
            string containerName,
            string blobName,
            StorageSharedKeyCredential sharedKeyCredentials = default,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigDefault.BlobServiceEndpoint}?{sasCredentials ?? GetNewBlobServiceSasCredentialsBlob(containerName: containerName, blobName: blobName, sharedKeyCredentials: sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_BlobServiceIdentitySas_Blob(
            string containerName,
            string blobName,
            UserDelegationKey userDelegationKey,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigOAuth.BlobServiceEndpoint}?{sasCredentials ?? GetNewBlobServiceIdentitySasCredentialsBlob(containerName: containerName, blobName: blobName, userDelegationKey: userDelegationKey, accountName: TestConfigOAuth.AccountName)}"),
                    GetOptions()));

        public BlobServiceClient GetServiceClient_BlobServiceSas_Snapshot(
            string containerName,
            string blobName,
            string snapshot,
            StorageSharedKeyCredential sharedKeyCredentials = default,
            BlobSasQueryParameters sasCredentials = default)
            => InstrumentClient(
                new BlobServiceClient(
                    new Uri($"{TestConfigDefault.BlobServiceEndpoint}?{sasCredentials ?? GetNewBlobServiceSasCredentialsSnapshot(containerName: containerName, blobName: blobName, snapshot: snapshot, sharedKeyCredentials: sharedKeyCredentials ?? GetNewSharedKeyCredentials())}"),
                    GetOptions()));

        public IDisposable GetNewContainer(
            out BlobContainerClient container,
            BlobServiceClient service = default,
            string containerName = default,
            IDictionary<string, string> metadata = default,
            PublicAccessType publicAccessType = PublicAccessType.None,
            bool premium = default)
        {
            containerName ??= GetNewContainerName();
            service ??= GetServiceClient_SharedKey();
            container = InstrumentClient(service.GetBlobContainerClient(containerName));

            if (publicAccessType == PublicAccessType.None)
            {
                publicAccessType = premium ? PublicAccessType.None : PublicAccessType.BlobContainer;
            }

            return new DisposingContainer(
                container,
                metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                publicAccessType);
        }

        public StorageSharedKeyCredential GetNewSharedKeyCredentials()
            => new StorageSharedKeyCredential(
                    TestConfigDefault.AccountName,
                    TestConfigDefault.AccountKey);

        public SasQueryParameters GetNewAccountSasCredentials(StorageSharedKeyCredential sharedKeyCredentials = default)
            => new AccountSasBuilder
            {
                Protocol = SasProtocol.None,
                Services = new AccountSasServices { Blobs = true }.ToString(),
                ResourceTypes = new AccountSasResourceTypes { BlobContainer = true, Object = true }.ToString(),
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new BlobContainerSasPermissions { Read = true, Add = true, Create = true, Write = true, Delete = true, List = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(sharedKeyCredentials);

        public BlobSasQueryParameters GetNewBlobServiceSasCredentialsContainer(string containerName, StorageSharedKeyCredential sharedKeyCredentials = default)
            => new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Protocol = SasProtocol.None,
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new BlobContainerSasPermissions { Read = true, Add = true, Create = true, Write = true, Delete = true, List = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(sharedKeyCredentials ?? GetNewSharedKeyCredentials());

        public BlobSasQueryParameters GetNewBlobServiceIdentitySasCredentialsContainer(string containerName, UserDelegationKey userDelegationKey, string accountName)
            => new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Protocol = SasProtocol.None,
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new BlobContainerSasPermissions { Read = true, Add = true, Create = true, Write = true, Delete = true, List = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(userDelegationKey, accountName);

        public BlobSasQueryParameters GetNewBlobServiceSasCredentialsBlob(string containerName, string blobName, StorageSharedKeyCredential sharedKeyCredentials = default)
            => new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Protocol = SasProtocol.None,
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new BlobSasPermissions { Read = true, Add = true, Create = true, Write = true, Delete = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(sharedKeyCredentials ?? GetNewSharedKeyCredentials());

        public BlobSasQueryParameters GetNewBlobServiceIdentitySasCredentialsBlob(string containerName, string blobName, UserDelegationKey userDelegationKey, string accountName)
            => new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Protocol = SasProtocol.None,
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new BlobSasPermissions { Read = true, Add = true, Create = true, Write = true, Delete = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(userDelegationKey, accountName);

        public BlobSasQueryParameters GetNewBlobServiceSasCredentialsSnapshot(string containerName, string blobName, string snapshot, StorageSharedKeyCredential sharedKeyCredentials = default)
            => new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Snapshot = snapshot,
                Protocol = SasProtocol.None,
                StartTime = Recording.UtcNow.AddHours(-1),
                ExpiryTime = Recording.UtcNow.AddHours(+1),
                Permissions = new SnapshotSasPermissions { Read = true, Write = true, Delete = true }.ToString(),
                IPRange = new IPRange(IPAddress.None, IPAddress.None)
            }.ToSasQueryParameters(sharedKeyCredentials ?? GetNewSharedKeyCredentials());

        public async Task<PageBlobClient> CreatePageBlobClientAsync(BlobContainerClient container, long size)
        {
            PageBlobClient blob = InstrumentClient(container.GetPageBlobClient(GetNewBlobName()));
            await blob.CreateAsync(size, 0).ConfigureAwait(false);
            return blob;
        }

        public string ToBase64(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(bytes);
        }

        public CustomerProvidedKey GetCustomerProvidedKey()
        {
            var bytes = new byte[32];
            Recording.Random.NextBytes(bytes);
            return new CustomerProvidedKey(bytes);
        }

        public Uri GetHttpsUri(Uri uri)
        {
            var uriBuilder = new UriBuilder(uri)
            {
                Scheme = Constants.Https,
                Port = Constants.HttpPort
            };
            return uriBuilder.Uri;
        }

        //TODO consider removing this.
        public async Task<string> SetupBlobMatchCondition(BlobBaseClient blob, string match)
        {
            if (match == ReceivedETag)
            {
                Response<BlobProperties> headers = await blob.GetPropertiesAsync();
                return headers.Value.ETag.ToString();
            }
            else
            {
                return match;
            }
        }

        //TODO consider removing this.
        public async Task<string> SetupBlobLeaseCondition(BlobBaseClient blob, string leaseId, string garbageLeaseId)
        {
            Lease lease = null;
            if (leaseId == ReceivedLeaseId || leaseId == garbageLeaseId)
            {
                lease = await InstrumentClient(blob.GetLeaseClient(Recording.Random.NewGuid().ToString())).AcquireAsync(LeaseClient.InfiniteLeaseDuration);
            }
            return leaseId == ReceivedLeaseId ? lease.LeaseId : leaseId;
        }

        //TODO consider removing this.
        public async Task<string> SetupContainerLeaseCondition(BlobContainerClient container, string leaseId, string garbageLeaseId)
        {
            Lease lease = null;
            if (leaseId == ReceivedLeaseId || leaseId == garbageLeaseId)
            {
                lease = await InstrumentClient(container.GetLeaseClient(Recording.Random.NewGuid().ToString())).AcquireAsync(LeaseClient.InfiniteLeaseDuration);
            }
            return leaseId == ReceivedLeaseId ? lease.LeaseId : leaseId;
        }

        public SignedIdentifier[] BuildSignedIdentifiers() =>
            new[]
            {
                new SignedIdentifier
                {
                    Id = GetNewString(),
                    AccessPolicy =
                        new AccessPolicy
                        {
                            Start = Recording.UtcNow.AddHours(-1),
                            Expiry =  Recording.UtcNow.AddHours(1),
                            Permission = "rw"
                        }
                }
            };

        public async Task EnableSoftDelete()
        {
            BlobServiceClient service = GetServiceClient_SharedKey();
            Response<BlobServiceProperties> properties = await service.GetPropertiesAsync();
            properties.Value.DeleteRetentionPolicy = new RetentionPolicy
            {
                Enabled = true,
                Days = 2
            };
            await service.SetPropertiesAsync(properties);

            do
            {
                await Delay(250);
                properties = await service.GetPropertiesAsync();
            } while (!properties.Value.DeleteRetentionPolicy.Enabled);
        }

        public async Task DisableSoftDelete()
        {
            BlobServiceClient service = GetServiceClient_SharedKey();
            Response<BlobServiceProperties> properties = await service.GetPropertiesAsync();
            properties.Value.DeleteRetentionPolicy = new RetentionPolicy
            {
                Enabled = false
            };
            await service.SetPropertiesAsync(properties);

            do
            {
                await Delay(250);
                properties = await service.GetPropertiesAsync();
            } while (properties.Value.DeleteRetentionPolicy.Enabled);
        }

        private class DisposingContainer : IDisposable
        {
            public BlobContainerClient ContainerClient { get; }

            public DisposingContainer(BlobContainerClient container, IDictionary<string, string> metadata, PublicAccessType publicAccessType = default)
            {
                container.CreateAsync(metadata: metadata, publicAccessType: publicAccessType).Wait();

                ContainerClient = container;
            }

            public void Dispose()
            {
                if (ContainerClient != null)
                {
                    try
                    {
                        ContainerClient.DeleteAsync().Wait();
                    }
                    catch
                    {
                        // swallow the exception to avoid hiding another test failure
                    }
                }
            }
        }
    }
}
