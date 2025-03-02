﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Storage.Blobs.Models
{
    internal class GetBlobsAsyncCollection : StorageCollectionEnumerator<BlobItem>
    {
        private readonly BlobContainerClient _client;
        private readonly GetBlobsOptions? _options;

        public GetBlobsAsyncCollection(
            BlobContainerClient client,
            GetBlobsOptions? options)
        {
            _client = client;
            _options = options;
        }

        public override async ValueTask<Page<BlobItem>> GetNextPageAsync(
            string continuationToken,
            int? pageSizeHint,
            bool isAsync,
            CancellationToken cancellationToken)
        {
            Task<Response<BlobsFlatSegment>> task = _client.GetBlobsInternal(
                continuationToken,
                _options,
                pageSizeHint,
                isAsync,
                cancellationToken);
            Response<BlobsFlatSegment> response = isAsync ?
                await task.ConfigureAwait(false) :
                task.EnsureCompleted();

            return new Page<BlobItem>(
                response.Value.BlobItems.ToArray(),
                response.Value.NextMarker,
                response.GetRawResponse());
        }
    }
}
