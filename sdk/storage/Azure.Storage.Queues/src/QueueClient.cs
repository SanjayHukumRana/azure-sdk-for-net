﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Common;
using Azure.Storage.Queues.Models;
using Metadata = System.Collections.Generic.IDictionary<string, string>;

namespace Azure.Storage.Queues
{
    /// <summary>
    /// A QueueClient represents a URI to the Azure Storage Queue service allowing you to manipulate a queue.
    /// </summary>
    public class QueueClient
    {
        /// <summary>
        /// The Uri endpoint used by the object.
        /// </summary>
        private readonly Uri _uri;

        /// <summary>
        /// Gets the Uri endpoint used by the object.
        /// </summary>
        public virtual Uri Uri => _uri;

        /// <summary>
        /// The Uri endpoint used by the object's messages.
        /// </summary>
        private readonly Uri _messagesUri;

        /// <summary>
        /// Gets the Uri endpoint used by the object's messages.
        /// </summary>
        protected virtual Uri MessagesUri => _messagesUri;

        /// <summary>
        /// The HttpPipeline used to send REST requests.
        /// </summary>
        private readonly HttpPipeline _pipeline;

        /// <summary>
        /// Gets the HttpPipeline used to send REST requests.
        /// </summary>
        internal virtual HttpPipeline Pipeline => _pipeline;

        /// <summary>
        /// QueueMaxMessagesPeek indicates the maximum number of messages
        /// you can retrieve with each call to Peek.
        /// </summary>
        public const int MaxMessagesPeek = Constants.Queue.MaxMessagesDequeue;

        /// <summary>
        /// QueueMessageMaxBytes indicates the maximum number of bytes allowed for a message's UTF-8 text.
        /// </summary>
        public const int MessageMaxBytes = Constants.Queue.QueueMessageMaxBytes;

        /// <summary>
        /// The Storage account name corresponding to the queue client.
        /// </summary>
        private string _accountName;

        /// <summary>
        /// Gets the Storage account name corresponding to the queue client.
        /// </summary>
        public virtual string AccountName
        {
            get
            {
                SetNameFieldsIfNull();
                return _accountName;
            }
        }

        /// <summary>
        /// The name of the queue.
        /// </summary>
        private string _name;

        /// <summary>
        /// Gets the name of the queue.
        /// </summary>
        public virtual string Name
        {
            get
            {
                SetNameFieldsIfNull();
                return _name;
            }
        }

        #region ctors
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class for mocking.
        /// </summary>
        protected QueueClient()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="connectionString">
        /// A connection string includes the authentication information
        /// required for your application to access data in an Azure Storage
        /// account at runtime.
        ///
        /// For more information, <see href="https://docs.microsoft.com/en-us/azure/storage/common/storage-configure-connection-string"/>.
        /// </param>
        /// <param name="queueName">
        /// The name of the queue in the storage account to reference.
        /// </param>
        public QueueClient(string connectionString, string queueName)
            : this(connectionString, queueName, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="connectionString">
        /// A connection string includes the authentication information
        /// required for your application to access data in an Azure Storage
        /// account at runtime.
        ///
        /// For more information, <see href="https://docs.microsoft.com/en-us/azure/storage/common/storage-configure-connection-string"/>.
        /// </param>
        /// <param name="queueName">
        /// The name of the queue in the storage account to reference.
        /// </param>
        /// <param name="options">
        /// Optional client options that define the transport pipeline
        /// policies for authentication, retries, etc., that are applied to
        /// every request.
        /// </param>
        public QueueClient(string connectionString, string queueName, QueueClientOptions options)
        {
            var conn = StorageConnectionString.Parse(connectionString);
            var builder =
                new QueueUriBuilder(conn.QueueEndpoint)
                {
                    QueueName = queueName
                };
            _uri = builder.ToUri();
            _messagesUri = _uri.AppendToPath(Constants.Queue.MessagesUri);
            options ??= new QueueClientOptions();
            _pipeline = options.Build(conn.Credentials);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="queueUri">
        /// A <see cref="Uri"/> referencing the queue that includes the
        /// name of the account, and the name of the queue.
        /// </param>
        /// <param name="options">
        /// Optional client options that define the transport pipeline
        /// policies for authentication, retries, etc., that are applied to
        /// every request.
        /// </param>
        public QueueClient(Uri queueUri, QueueClientOptions options = default)
            : this(queueUri, (HttpPipelinePolicy)null, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="queueUri">
        /// A <see cref="Uri"/> referencing the queue that includes the
        /// name of the account, and the name of the queue.
        /// </param>
        /// <param name="credential">
        /// The shared key credential used to sign requests.
        /// </param>
        /// <param name="options">
        /// Optional client options that define the transport pipeline
        /// policies for authentication, retries, etc., that are applied to
        /// every request.
        /// </param>
        public QueueClient(Uri queueUri, StorageSharedKeyCredential credential, QueueClientOptions options = default)
            : this(queueUri, credential.AsPolicy(), options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="queueUri">
        /// A <see cref="Uri"/> referencing the queue that includes the
        /// name of the account, and the name of the queue.
        /// </param>
        /// <param name="credential">
        /// The token credential used to sign requests.
        /// </param>
        /// <param name="options">
        /// Optional client options that define the transport pipeline
        /// policies for authentication, retries, etc., that are applied to
        /// every request.
        /// </param>
        public QueueClient(Uri queueUri, TokenCredential credential, QueueClientOptions options = default)
            : this(queueUri, credential.AsPolicy(), options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="queueUri">
        /// A <see cref="Uri"/> referencing the queue that includes the
        /// name of the account, and the name of the queue.
        /// </param>
        /// <param name="authentication">
        /// An optional authentication policy used to sign requests.
        /// </param>
        /// <param name="options">
        /// Optional client options that define the transport pipeline
        /// policies for authentication, retries, etc., that are applied to
        /// every request.
        /// </param>
        internal QueueClient(Uri queueUri, HttpPipelinePolicy authentication, QueueClientOptions options)
        {
            _uri = queueUri;
            _messagesUri = queueUri.AppendToPath(Constants.Queue.MessagesUri);
            options ??= new QueueClientOptions();
            _pipeline = options.Build(authentication);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/>
        /// class.
        /// </summary>
        /// <param name="queueUri">
        /// A <see cref="Uri"/> referencing the queue that includes the
        /// name of the account, and the name of the queue.
        /// </param>
        /// <param name="pipeline">
        /// The transport pipeline used to send every request.
        /// </param>
        internal QueueClient(Uri queueUri, HttpPipeline pipeline)
        {
            _uri = queueUri;
            _messagesUri = queueUri.AppendToPath(Constants.Queue.MessagesUri);
            _pipeline = pipeline;
        }
        #endregion ctors

        /// <summary>
        /// Sets the various name fields if they are currently null.
        /// </summary>
        private void SetNameFieldsIfNull()
        {
            if (_name == null || _accountName == null)
            {
                var builder = new QueueUriBuilder(Uri);
                _name = builder.QueueName;
                _accountName = builder.AccountName;
            }
        }

        #region Create
        /// <summary>
        /// Creates a queue.
        ///
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/create-queue4"/>.
        /// </summary>
        /// <param name="metadata">
        /// Optional <see cref="Metadata"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response" />
        /// </returns>
        public virtual Response Create(
            Metadata metadata = default,
            CancellationToken cancellationToken = default) =>
            CreateInternal(
                metadata,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Creates a queue.
        ///
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/create-queue4"/>.
        /// </summary>
        /// <param name="metadata">
        /// Optional <see cref="Metadata"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual async Task<Response> CreateAsync(
            Metadata metadata = default,
            CancellationToken cancellationToken = default) =>
            await CreateInternal(
                metadata,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Creates a queue.
        ///
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/create-queue4"/>.
        /// </summary>
        /// <param name="metadata">
        /// Optional <see cref="Metadata"/>.
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        private async Task<Response> CreateInternal(
            Metadata metadata,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.CreateAsync(
                        Pipeline,
                        Uri,
                        metadata: metadata,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion Create

        #region Delete
        /// <summary>
        /// Deletes a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-queue3"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual Response Delete(
            CancellationToken cancellationToken = default) =>
            DeleteInternal(
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Deletes a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-queue3"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual async Task<Response> DeleteAsync(
            CancellationToken cancellationToken = default) =>
            await DeleteInternal(
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Deletes a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-queue3"/>.
        /// </summary>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        private async Task<Response> DeleteInternal(
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.DeleteAsync(
                        Pipeline,
                        Uri,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion Delete

        #region GetProperties
        /// <summary>
        /// Retrieves queue properties and user-defined metadata and properties on the specified queue.
        /// Metadata is associated with the queue as name-values pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-metadata"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{QueueProperties}"/>
        /// </returns>
        public virtual Response<QueueProperties> GetProperties(
            CancellationToken cancellationToken = default) =>
            GetPropertiesInternal(
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Retrieves queue properties and user-defined metadata and properties on the specified queue.
        /// Metadata is associated with the queue as name-values pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-metadata"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{QueueProperties}"/>
        /// </returns>
        public virtual async Task<Response<QueueProperties>> GetPropertiesAsync(
            CancellationToken cancellationToken = default) =>
            await GetPropertiesInternal(
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Retrieves queue properties and user-defined metadata and properties on the specified queue.
        /// Metadata is associated with the queue as name-values pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-metadata"/>.
        /// </summary>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{QueueProperties}"/>
        /// </returns>
        private async Task<Response<QueueProperties>> GetPropertiesInternal(
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.GetPropertiesAsync(
                        Pipeline,
                        Uri,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion GetProperties

        #region SetMetadata
        /// <summary>
        /// Sets user-defined metadata on the specified queue. Metadata is associated with the queue as name-value pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-metadata"/>.
        /// </summary>
        /// <param name="metadata">
        /// <see cref="Metadata"/>
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual Response SetMetadata(
            Metadata metadata,
            CancellationToken cancellationToken = default) =>
            SetMetadataInternal(
                metadata,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Sets user-defined metadata on the specified queue. Metadata is associated with the queue as name-value pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-metadata"/>.
        /// </summary>
        /// <param name="metadata">
        /// <see cref="Metadata"/>
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual async Task<Response> SetMetadataAsync(
            Metadata metadata,
            CancellationToken cancellationToken = default) =>
            await SetMetadataInternal(
                metadata,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Sets user-defined metadata on the specified queue. Metadata is associated with the queue as name-value pairs.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-metadata"/>.
        /// </summary>
        /// <param name="metadata">
        /// <see cref="Metadata"/>
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        private async Task<Response> SetMetadataInternal(
            Metadata metadata,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.SetMetadataAsync(
                        Pipeline,
                        Uri,
                        metadata: metadata,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion SetMetadata

        #region GetAccessPolicy
        /// <summary>
        /// Returns details about any stored access policies specified on the queue that may be used with
        /// Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-acl"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> of <see cref="IEnumerable{SignedIdentifier}" />
        /// </returns>
        public virtual Response<IEnumerable<SignedIdentifier>> GetAccessPolicy(
            CancellationToken cancellationToken = default) =>
            GetAccessPolicyInternal(
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Returns details about any stored access policies specified on the queue that may be used with
        /// Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-acl"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> of <see cref="IEnumerable{SignedIdentifier}" />
        /// </returns>
        public virtual async Task<Response<IEnumerable<SignedIdentifier>>> GetAccessPolicyAsync(
            CancellationToken cancellationToken = default) =>
            await GetAccessPolicyInternal(
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Returns details about any stored access policies specified on the queue that may be used with
        /// Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-queue-acl"/>.
        /// </summary>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> of <see cref="IEnumerable{SignedIdentifier}" />
        /// </returns>
        private async Task<Response<IEnumerable<SignedIdentifier>>> GetAccessPolicyInternal(
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.GetAccessPolicyAsync(
                        Pipeline,
                        Uri,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion GetAccessPolicy

        #region SetAccessPolicy
        /// <summary>
        /// SetAccessPolicyAsync sets stored access policies for the queue that may be used with Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-acl"/>.
        /// </summary>
        /// <param name="permissions">
        /// IEnumerable of <see cref="SignedIdentifier"/>
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual Response SetAccessPolicy(
            IEnumerable<SignedIdentifier> permissions,
            CancellationToken cancellationToken = default) =>
            SetAccessPolicyInternal(
                permissions,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// SetAccessPolicyAsync sets stored access policies for the queue that may be used with Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-acl"/>.
        /// </summary>
        /// <param name="permissions">
        /// IEnumerable of <see cref="SignedIdentifier"/>
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual async Task<Response> SetAccessPolicyAsync(
            IEnumerable<SignedIdentifier> permissions,
            CancellationToken cancellationToken = default) =>
            await SetAccessPolicyInternal(
                permissions,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// SetAccessPolicyInternal sets stored access policies for the queue that may be used with Shared Access Signatures.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/set-queue-acl"/>.
        /// </summary>
        /// <param name="permissions">
        /// IEnumerable of <see cref="SignedIdentifier"/>
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        private async Task<Response> SetAccessPolicyInternal(
            IEnumerable<SignedIdentifier> permissions,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"{nameof(Uri)}: {Uri}");
                try
                {
                    return await QueueRestClient.Queue.SetAccessPolicyAsync(
                        Pipeline,
                        Uri,
                        permissions: permissions,
                        async: async,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion SetAccessPolicy

        #region ClearMessages
        /// <summary>
        /// Deletes all messages from a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/clear-messages"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual Response ClearMessages(
            CancellationToken cancellationToken = default) =>
            ClearMessagesInternal(
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Deletes all messages from a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/clear-messages"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        public virtual async Task<Response> ClearMessagesAsync(
            CancellationToken cancellationToken = default) =>
            await ClearMessagesInternal(
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Deletes all messages from a queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/clear-messages"/>.
        /// </summary>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>
        /// </returns>
        private async Task<Response> ClearMessagesInternal(
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message: $"Uri: {MessagesUri}");
                try
                {
                    return await QueueRestClient.Messages.ClearAsync(
                        Pipeline,
                        MessagesUri,
                        async: async,
                        operationName: Constants.Queue.ClearMessagesOperationName,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion ClearMessages

        #region EnqueueMessage
        /// <summary>
        /// Adds a new message to the back of a queue. The visibility timeout specifies how long the message should be invisible
        /// to Dequeue and Peek operations. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/put-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Message text.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Visibility timeout.  Optional with a default value of 0.  Cannot be larger than 7 days.
        /// </param>
        /// <param name="timeToLive">
        /// Optional. Specifies the time-to-live interval for the message
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{EnqueuedMessage}"/>
        /// </returns>
        public virtual Response<EnqueuedMessage> EnqueueMessage(
            string messageText,
            TimeSpan? visibilityTimeout = default,
            TimeSpan? timeToLive = default,
            CancellationToken cancellationToken = default) =>
            EnqueueMessageInternal(
                messageText,
                visibilityTimeout,
                timeToLive,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Adds a new message to the back of a queue. The visibility timeout specifies how long the message should be invisible
        /// to Dequeue and Peek operations. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/put-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Message text.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Visibility timeout.  Optional with a default value of 0.  Cannot be larger than 7 days.
        /// </param>
        /// <param name="timeToLive">
        /// Optional. Specifies the time-to-live interval for the message
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{EnqueuedMessage}"/>
        /// </returns>
        public virtual async Task<Response<EnqueuedMessage>> EnqueueMessageAsync(
            string messageText,
            TimeSpan? visibilityTimeout = default,
            TimeSpan? timeToLive = default,
            CancellationToken cancellationToken = default) =>
            await EnqueueMessageInternal(
                messageText,
                visibilityTimeout,
                timeToLive,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Adds a new message to the back of a queue. The visibility timeout specifies how long the message should be invisible
        /// to Dequeue and Peek operations. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/put-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Message text.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Visibility timeout.  Optional with a default value of 0.  Cannot be larger than 7 days.
        /// </param>
        /// <param name="timeToLive">
        /// Optional. Specifies the time-to-live interval for the message
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{EnqueuedMessage}"/>
        /// </returns>
        private async Task<Response<EnqueuedMessage>> EnqueueMessageInternal(
            string messageText,
            TimeSpan? visibilityTimeout,
            TimeSpan? timeToLive,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message:
                    $"Uri: {MessagesUri}\n" +
                    $"{nameof(visibilityTimeout)}: {visibilityTimeout}\n" +
                    $"{nameof(timeToLive)}: {timeToLive}");
                try
                {
                    Response<IEnumerable<EnqueuedMessage>> messages =
                        await QueueRestClient.Messages.EnqueueAsync(
                            Pipeline,
                            MessagesUri,
                            message: new QueueMessage { MessageText = messageText },
                            visibilitytimeout: (int?)visibilityTimeout?.TotalSeconds,
                            messageTimeToLive: (int?)timeToLive?.TotalSeconds,
                            async: async,
                            operationName: Constants.Queue.EnqueueMessageOperationName,
                            cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    // The service returns a sequence of messages, but the
                    // sequence only ever has one value so we'll unwrap it
                    return Response.FromValue(messages.GetRawResponse(), messages.Value.FirstOrDefault());
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion EnqueueMessage

        #region DequeueMessages
        /// <summary>
        /// Retrieves one or more messages from the front of the queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to retrieve from the queue, up to a maximum of 32.
        /// If fewer are visible, the visible messages are returned. By default, a single message is retrieved from the queue with this operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Optional. Specifies the new visibility timeout value, in seconds, relative to server time. The default value is 30 seconds.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="DequeuedMessage"/>
        /// </returns>
        public virtual Response<DequeuedMessage[]> DequeueMessages(
            int? maxMessages = default,
            TimeSpan? visibilityTimeout = default,
            CancellationToken cancellationToken = default) =>
            DequeueMessagesInternal(
                maxMessages,
                visibilityTimeout,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Retrieves one or more messages from the front of the queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to retrieve from the queue, up to a maximum of 32.
        /// If fewer are visible, the visible messages are returned. By default, a single message is retrieved from the queue with this operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Optional. Specifies the new visibility timeout value, in seconds, relative to server time. The default value is 30 seconds.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="DequeuedMessage"/>
        /// </returns>
        public virtual async Task<Response<DequeuedMessage[]>> DequeueMessagesAsync(
            int? maxMessages = default,
            TimeSpan? visibilityTimeout = default,
            CancellationToken cancellationToken = default) =>
            await DequeueMessagesInternal(
                maxMessages,
                visibilityTimeout,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Retrieves one or more messages from the front of the queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/get-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to retrieve from the queue, up to a maximum of 32.
        /// If fewer are visible, the visible messages are returned. By default, a single message is retrieved from the queue with this operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Optional. Specifies the new visibility timeout value, in seconds, relative to server time. The default value is 30 seconds.
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="DequeuedMessage"/>
        /// </returns>
        private async Task<Response<DequeuedMessage[]>> DequeueMessagesInternal(
            int? maxMessages,
            TimeSpan? visibilityTimeout,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message:
                    $"Uri: {MessagesUri}\n" +
                    $"{nameof(maxMessages)}: {maxMessages}\n" +
                    $"{nameof(visibilityTimeout)}: {visibilityTimeout}");
                try
                {
                    var dequeuedMessage = await QueueRestClient.Messages.DequeueAsync(
                        Pipeline,
                        MessagesUri,
                        numberOfMessages: maxMessages,
                        visibilitytimeout: (int?)visibilityTimeout?.TotalSeconds,
                        async: async,
                        operationName: Constants.Queue.DequeueMessageOperationName,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return Response.FromValue(dequeuedMessage.GetRawResponse(), dequeuedMessage.Value.ToArray());
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion DequeueMessages

        #region PeekMessages
        /// <summary>
        /// Retrieves one or more messages from the front of the queue but does not alter the visibility of the message.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/peek-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to peek from the queue, up to a maximum of 32.
        /// By default, a single message is peeked from the queue with this operation.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="PeekedMessage"/>
        /// </returns>
        public virtual Response<PeekedMessage[]> PeekMessages(
            int? maxMessages = default,
            CancellationToken cancellationToken = default) =>
            PeekMessagesInternal(
                maxMessages,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Retrieves one or more messages from the front of the queue but does not alter the visibility of the message.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/peek-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to peek from the queue, up to a maximum of 32.
        /// By default, a single message is peeked from the queue with this operation.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="PeekedMessage"/>
        /// </returns>
        public virtual async Task<Response<PeekedMessage[]>> PeekMessagesAsync(
            int? maxMessages = default,
            CancellationToken cancellationToken = default) =>
            await PeekMessagesInternal(
                maxMessages,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Retrieves one or more messages from the front of the queue but does not alter the visibility of the message.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/peek-messages"/>.
        /// </summary>
        /// <param name="maxMessages">
        /// Optional. A nonzero integer value that specifies the number of messages to peek from the queue, up to a maximum of 32.
        /// By default, a single message is peeked from the queue with this operation.
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>
        /// </param>
        /// <returns>
        /// <see cref="Response{T}"/> where T is an array of <see cref="PeekedMessage"/>
        /// </returns>
        private async Task<Response<PeekedMessage[]>> PeekMessagesInternal(
            int? maxMessages,
            bool async,
            CancellationToken cancellationToken)
        {
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message:
                    $"Uri: {MessagesUri}\n" +
                    $"{nameof(maxMessages)}: {maxMessages}");
                try
                {
                    var peekedMessages = await QueueRestClient.Messages.PeekAsync(
                        Pipeline,
                        MessagesUri,
                        numberOfMessages: maxMessages,
                        async: async,
                        operationName: Constants.Queue.PeekMessagesOperationName,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    return Response.FromValue(peekedMessages.GetRawResponse(), peekedMessages.Value.ToArray());
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion PeekMessages

        /// <summary>
        /// Get the URI to a specific message given its ID.
        /// </summary>
        /// <param name="messageId">ID of the message.</param>
        /// <returns>URI to the given message.</returns>
        private Uri GetMessageUri(string messageId) =>
            MessagesUri.AppendToPath(messageId.ToString(CultureInfo.InvariantCulture));

        #region DeleteMessage
        /// <summary>
        /// Permanently removes the specified message from its queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-message2"/>.
        /// </summary>
        /// <param name="messageId">ID of the message to delete.</param>
        /// <param name="popReceipt">
        /// Required. A valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>.
        /// </returns>
        public virtual Response DeleteMessage(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default) =>
            DeleteMessageInternal(
                messageId,
                popReceipt,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Permanently removes the specified message from its queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-message2"/>.
        /// </summary>
        /// <param name="messageId">ID of the message to delete.</param>
        /// <param name="popReceipt">
        /// Required. A valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>.
        /// </returns>
        public virtual async Task<Response> DeleteMessageAsync(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default) =>
            await DeleteMessageInternal(
                messageId,
                popReceipt,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Permanently removes the specified message from its queue.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/delete-message2"/>.
        /// </summary>
        /// <param name="messageId">ID of the message to delete.</param>
        /// <param name="popReceipt">
        /// Required. A valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response"/>.
        /// </returns>
        private async Task<Response> DeleteMessageInternal(
            string messageId,
            string popReceipt,
            bool async,
            CancellationToken cancellationToken)
        {
            Uri uri = GetMessageUri(messageId);
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message:
                    $"Uri: {uri}\n" +
                    $"{nameof(popReceipt)}: {popReceipt}");
                try
                {
                    return await QueueRestClient.MessageId.DeleteAsync(
                        Pipeline,
                        uri,
                        popReceipt: popReceipt,
                        async: async,
                        operationName: Constants.Queue.DeleteMessageOperationName,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion DeleteMessage

        #region UpdateMessage
        /// <summary>
        /// Changes a message's visibility timeout and contents. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/update-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Updated message text.
        /// </param>
        /// <param name="messageId">ID of the message to update.</param>
        /// <param name="popReceipt">
        /// Required. Specifies the valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Required. Specifies the new visibility timeout value, in seconds, relative to server time. The new value must be larger than
        /// or equal to 0, and cannot be larger than 7 days. The visibility timeout of a message cannot be set to a value later than the
        /// expiry time. A message can be updated until it has been deleted or has expired.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{UpdatedMessage}"/>.
        /// </returns>
        public virtual Response<UpdatedMessage> UpdateMessage(
            string messageText,
            string messageId,
            string popReceipt,
            TimeSpan visibilityTimeout = default,
            CancellationToken cancellationToken = default) =>
            UpdateMessageInternal(
                messageText,
                messageId,
                popReceipt,
                visibilityTimeout,
                false, // async
                cancellationToken)
                .EnsureCompleted();

        /// <summary>
        /// Changes a message's visibility timeout and contents. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/update-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Updated message text.
        /// </param>
        /// <param name="messageId">ID of the message to update.</param>
        /// <param name="popReceipt">
        /// Required. Specifies the valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Required. Specifies the new visibility timeout value, in seconds, relative to server time. The new value must be larger than
        /// or equal to 0, and cannot be larger than 7 days. The visibility timeout of a message cannot be set to a value later than the
        /// expiry time. A message can be updated until it has been deleted or has expired.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{UpdatedMessage}"/>.
        /// </returns>
        public virtual async Task<Response<UpdatedMessage>> UpdateMessageAsync(
            string messageText,
            string messageId,
            string popReceipt,
            TimeSpan visibilityTimeout = default,
            CancellationToken cancellationToken = default) =>
            await UpdateMessageInternal(
                messageText,
                messageId,
                popReceipt,
                visibilityTimeout,
                true, // async
                cancellationToken)
                .ConfigureAwait(false);

        /// <summary>
        /// Changes a message's visibility timeout and contents. The message content must be a UTF-8 encoded string that is up to 64KB in size.
        /// For more information, see <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/update-message"/>.
        /// </summary>
        /// <param name="messageText">
        /// Updated message text.
        /// </param>
        /// <param name="messageId">ID of the message to update.</param>
        /// <param name="popReceipt">
        /// Required. Specifies the valid pop receipt value returned from an earlier call to the Get Messages or Update Message operation.
        /// </param>
        /// <param name="visibilityTimeout">
        /// Required. Specifies the new visibility timeout value, in seconds, relative to server time. The new value must be larger than
        /// or equal to 0, and cannot be larger than 7 days. The visibility timeout of a message cannot be set to a value later than the
        /// expiry time. A message can be updated until it has been deleted or has expired.
        /// </param>
        /// <param name="async">
        /// Whether to invoke the operation asynchronously.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional <see cref="CancellationToken"/>.
        /// </param>
        /// <returns>
        /// <see cref="Response{UpdatedMessage}"/>.
        /// </returns>
        private async Task<Response<UpdatedMessage>> UpdateMessageInternal(
            string messageText,
            string messageId,
            string popReceipt,
            TimeSpan visibilityTimeout,
            bool async,
            CancellationToken cancellationToken)
        {
            Uri uri = GetMessageUri(messageId);
            using (Pipeline.BeginLoggingScope(nameof(QueueClient)))
            {
                Pipeline.LogMethodEnter(
                    nameof(QueueClient),
                    message:
                    $"Uri: {uri}\n" +
                    $"{nameof(popReceipt)}: {popReceipt}" +
                    $"{nameof(visibilityTimeout)}: {visibilityTimeout}");
                try
                {
                    return await QueueRestClient.MessageId.UpdateAsync(
                        Pipeline,
                        uri,
                        message: new QueueMessage { MessageText = messageText },
                        popReceipt: popReceipt,
                        visibilitytimeout: (int)visibilityTimeout.TotalSeconds,
                        async: async,
                        operationName: Constants.Queue.UpdateMessageOperationName,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Pipeline.LogException(ex);
                    throw;
                }
                finally
                {
                    Pipeline.LogMethodExit(nameof(QueueClient));
                }
            }
        }
        #endregion UpdateMessage
    }
}
