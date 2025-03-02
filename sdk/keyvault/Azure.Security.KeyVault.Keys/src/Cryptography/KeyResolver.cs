﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Cryptography;
using Azure.Core.Http;
using Azure.Core.Pipeline;

namespace Azure.Security.KeyVault.Keys.Cryptography
{
    /// <summary>
    /// Azure Key Vault KeyResolver. This class resolves Key Vault Key Identifiers and
    /// Secret Identifiers to create <see cref="CryptographyClient"/> instances capable of performing
    /// cryptographic operations with the key. Secret Identifiers can only be resolved if the Secret is
    /// a byte array with a length matching one of the AES key lengths (128, 192, 256) and the
    /// content-type of the secret is application/octet-stream.
    /// </summary>
    public class KeyResolver : IKeyEncryptionKeyResolver
    {
        private readonly HttpPipeline  _pipeline;
        private readonly string _apiVersion;

        /// <summary>
        /// Protected constructor for mocking
        /// </summary>
        protected KeyResolver()
        {
        }

        /// <summary>
        /// Creates a new KeyResolver instance
        /// </summary>
        /// <param name="credential">A <see cref="TokenCredential"/> capable of providing an OAuth token used to authenticate to Key Vault.</param>
        public KeyResolver(TokenCredential credential)
            : this(credential, null)
        {
        }

        /// <summary>
        /// Creates a new KeyResolver instance
        /// </summary>
        /// <param name="credential">A <see cref="TokenCredential"/> capable of providing an OAuth token used to authenticate to Key Vault.</param>
        /// <param name="options">Options to configure the management of the requests sent to Key Vault for both the KeyResolver instance as well as all created instances of <see cref="CryptographyClient"/>.</param>
        public KeyResolver(TokenCredential credential, CryptographyClientOptions options)
        {
            Argument.AssertNotNull(credential, nameof(credential));

            options ??= new CryptographyClientOptions();

            _apiVersion = options.GetVersionString();

            _pipeline = HttpPipelineBuilder.Build(options,
                    new ChallengeBasedAuthenticationPolicy(credential));
        }

        /// <summary>
        /// Retrieves a <see cref="CryptographyClient"/> capable of performing cryptographic operations with the key represented by the specfiied keyId.
        /// </summary>
        /// <param name="keyId">The key idenitifier of the key used by the created <see cref="CryptographyClient"/> </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        /// <returns>A new <see cref="CryptographyClient"/> capable of performing cryptographic operations with the key represented by the specfiied keyId</returns>
        public virtual CryptographyClient Resolve(Uri keyId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(keyId, nameof(keyId));

            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Security.KeyVault.Keys.Cryptography.KeyResolver.Resolve");
            scope.AddAttribute("key", keyId);
            scope.Start();

            try
            {
                Argument.AssertNotNull(keyId, nameof(keyId));

                Key key = GetKey(keyId, cancellationToken);

                KeyVaultPipeline pipeline = new KeyVaultPipeline(keyId, _apiVersion, _pipeline);

                return (key != null) ? new CryptographyClient(key.KeyMaterial, pipeline) : new CryptographyClient(keyId, pipeline);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a <see cref="CryptographyClient"/> capable of performing cryptographic operations with the key represented by the specfiied keyId.
        /// </summary>
        /// <param name="keyId">The key idenitifier of the key used by the created <see cref="CryptographyClient"/> </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        /// <returns>A new <see cref="CryptographyClient"/> capable of performing cryptographic operations with the key represented by the specfiied keyId</returns>
        public virtual async Task<CryptographyClient> ResolveAsync(Uri keyId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(keyId, nameof(keyId));

            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Security.KeyVault.Keys.Cryptography.KeyResolver.Resolve");
            scope.AddAttribute("key", keyId);
            scope.Start();

            try
            {
                Argument.AssertNotNull(keyId, nameof(keyId));

                Key key = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

                KeyVaultPipeline pipeline = new KeyVaultPipeline(keyId, _apiVersion, _pipeline);

                return (key != null) ? new CryptographyClient(key.KeyMaterial, pipeline) : new CryptographyClient(keyId, pipeline);

            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc/>
        IKeyEncryptionKey IKeyEncryptionKeyResolver.Resolve(string keyId, CancellationToken cancellationToken)
        {
            return ((KeyResolver)this).Resolve(new Uri(keyId), cancellationToken);
        }

        /// <inheritdoc/>
        async Task<IKeyEncryptionKey> IKeyEncryptionKeyResolver.ResolveAsync(string keyId, CancellationToken cancellationToken)
        {
            return await ((KeyResolver)this).ResolveAsync(new Uri(keyId), cancellationToken).ConfigureAwait(false);
        }

        private Key GetKey(Uri keyId, CancellationToken cancellationToken)
        {
            using Request request = CreateGetRequest(keyId);

            Response response = _pipeline.SendRequest(request, cancellationToken);

            return KeyVaultIdentifier.Parse(keyId).Collection == KeyVaultIdentifier.SecretsCollection ? (Key)ParseResponse(response, new SecretKey()) : ParseResponse(response, new Key());
        }

        private async Task<Key> GetKeyAsync(Uri keyId, CancellationToken cancellationToken)
        {
            using Request request = CreateGetRequest(keyId);

            Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            return KeyVaultIdentifier.Parse(keyId).Collection == KeyVaultIdentifier.SecretsCollection ? (Key)ParseResponse(response, new SecretKey()) : ParseResponse(response, new Key());
        }

        private Response<T> ParseResponse<T>(Response response, T result)
            where T : IJsonDeserializable
        {
            switch (response.Status)
            {
                case 200:
                case 201:
                case 202:
                case 204:
                    result.Deserialize(response.ContentStream);
                    return Response.FromValue(response, result);
                default:
                    throw response.CreateRequestFailedException();
            }
        }

        private Request CreateGetRequest(Uri uri)
        {
            Request request = _pipeline.CreateRequest();

            request.Headers.Add(HttpHeader.Common.JsonContentType);
            request.Headers.Add(HttpHeader.Common.JsonAccept);
            request.Method = RequestMethod.Get;
            request.Uri.Reset(uri);

            request.Uri.AppendQuery("api-version", _apiVersion);

            return request;
        }

    }
}
