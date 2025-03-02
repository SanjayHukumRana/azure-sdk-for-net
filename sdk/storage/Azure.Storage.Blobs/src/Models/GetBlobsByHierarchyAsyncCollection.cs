﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Storage.Blobs.Models
{
    internal class GetBlobsByHierarchyAsyncCollection : StorageCollectionEnumerator<BlobHierarchyItem>
    {
        private readonly BlobContainerClient _client;
        private readonly GetBlobsOptions? _options;
        private readonly string _delimiter;

        public GetBlobsByHierarchyAsyncCollection(
            BlobContainerClient client,
            string delimiter,
            GetBlobsOptions? options)
        {
            _client = client;
            _delimiter = delimiter;
            _options = options;
        }

        public override async ValueTask<Page<BlobHierarchyItem>> GetNextPageAsync(
            string continuationToken,
            int? pageSizeHint,
            bool isAsync,
            CancellationToken cancellationToken)
        {
            Task<Response<BlobsHierarchySegment>> task = _client.GetBlobsByHierarchyInternal(
                continuationToken,
                _delimiter,
                _options,
                pageSizeHint,
                isAsync,
                cancellationToken);
            Response<BlobsHierarchySegment> response = isAsync ?
                await task.ConfigureAwait(false) :
                task.EnsureCompleted();

            var items = new List<BlobHierarchyItem>();
            items.AddRange(response.Value.BlobPrefixes.Select(p => new BlobHierarchyItem(p.Name, null)));
            items.AddRange(response.Value.BlobItems.Select(b => new BlobHierarchyItem(null, b)));
            return new Page<BlobHierarchyItem>(
                items.ToArray(),
                response.Value.NextMarker,
                response.GetRawResponse());
        }
    }
}
