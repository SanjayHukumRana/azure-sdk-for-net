﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Core.Http;
using Azure.Core.Pipeline;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Security.KeyVault
{
    internal class KeyVaultPipeline
    {
        private readonly Uri _vaultUri;
        private readonly HttpPipeline _pipeline;

        public KeyVaultPipeline(Uri vaultUri, string apiVersion, HttpPipeline pipeline)
        {
            _vaultUri = vaultUri;
            _pipeline = pipeline;

            ApiVersion = apiVersion;
        }

        public string ApiVersion { get; }

        public Uri CreateFirstPageUri(string path)
        {
            var firstPage = new RequestUriBuilder();
            firstPage.Reset(_vaultUri);

            firstPage.AppendPath(path);
            firstPage.AppendQuery("api-version", ApiVersion);

            return firstPage.ToUri();
        }

        public Uri CreateFirstPageUri(string path, params ValueTuple<string, string>[] queryParams)
        {
            var firstPage = new RequestUriBuilder();
            firstPage.Reset(_vaultUri);

            firstPage.AppendPath(path);
            firstPage.AppendQuery("api-version", ApiVersion);

            foreach ((string, string) tuple in queryParams)
            {
                firstPage.AppendQuery(tuple.Item1, tuple.Item2);
            }

            return firstPage.ToUri();
        }

        public Request CreateRequest(RequestMethod method, Uri uri)
        {
            Request request = _pipeline.CreateRequest();

            request.Headers.Add(HttpHeader.Common.JsonContentType);
            request.Headers.Add(HttpHeader.Common.JsonAccept);
            request.Method = method;
            request.Uri.Reset(uri);

            return request;
        }

        public Request CreateRequest(RequestMethod method, params string[] path)
        {
            Request request = _pipeline.CreateRequest();

            request.Headers.Add(HttpHeader.Common.JsonContentType);
            request.Headers.Add(HttpHeader.Common.JsonAccept);
            request.Method = method;
            request.Uri.Reset(_vaultUri);

            foreach (var p in path)
            {
                request.Uri.AppendPath(p);
            }

            request.Uri.AppendQuery("api-version", ApiVersion);

            return request;
        }

        public Response<T> CreateResponse<T>(Response response, T result)
            where T : IJsonDeserializable
        {
            result.Deserialize(response.ContentStream);
            return Response.FromValue(response, result);
        }

        public DiagnosticScope CreateScope(string name)
        {
            return _pipeline.Diagnostics.CreateScope(name);
        }

        public async Task<Page<T>> GetPageAsync<T>(Uri firstPageUri, string nextLink, Func<T> itemFactory, string operationName, CancellationToken cancellationToken)
                where T : IJsonDeserializable
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope(operationName);
            scope.Start();

            try
            {
                // if we don't have a nextLink specified, use firstPageUri
                if (nextLink != null)
                {
                    firstPageUri = new Uri(nextLink);
                }

                using Request request = CreateRequest(RequestMethod.Get, firstPageUri);
                Response response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                // read the respose
                KeyVaultPage<T> responseAsPage = new KeyVaultPage<T>(itemFactory);
                responseAsPage.Deserialize(response.ContentStream);

                // convert from the Page<T> to PageResponse<T>
                return new Page<T>(responseAsPage.Items.ToArray(), responseAsPage.NextLink?.ToString(), response);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        public Page<T> GetPage<T>(Uri firstPageUri, string nextLink, Func<T> itemFactory, string operationName, CancellationToken cancellationToken)
            where T : IJsonDeserializable
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope(operationName);
            scope.Start();

            try
            {
                // if we don't have a nextLink specified, use firstPageUri
                if (nextLink != null)
                {
                    firstPageUri = new Uri(nextLink);
                }

                using Request request = CreateRequest(RequestMethod.Get, firstPageUri);
                Response response = SendRequest(request, cancellationToken);

                // read the respose
                KeyVaultPage<T> responseAsPage = new KeyVaultPage<T>(itemFactory);
                responseAsPage.Deserialize(response.ContentStream);

                // convert from the Page<T> to PageResponse<T>
                return new Page<T>(responseAsPage.Items.ToArray(), responseAsPage.NextLink?.ToString(), response);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        public async Task<Response<TResult>> SendRequestAsync<TContent, TResult>(RequestMethod method, TContent content, Func<TResult> resultFactory, CancellationToken cancellationToken, params string[] path)
            where TContent : IJsonSerializable
            where TResult : IJsonDeserializable
        {
            using Request request = CreateRequest(method, path);
            request.Content = HttpPipelineRequestContent.Create(content.Serialize());

            Response response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            return CreateResponse(response, resultFactory());
        }

        public Response<TResult> SendRequest<TContent, TResult>(RequestMethod method, TContent content, Func<TResult> resultFactory, CancellationToken cancellationToken, params string[] path)
            where TContent : IJsonSerializable
            where TResult : IJsonDeserializable
        {
            using Request request = CreateRequest(method, path);
            request.Content = HttpPipelineRequestContent.Create(content.Serialize());

            Response response = SendRequest(request, cancellationToken);

            return CreateResponse(response, resultFactory());
        }

        public async Task<Response<TResult>> SendRequestAsync<TResult>(RequestMethod method, Func<TResult> resultFactory, CancellationToken cancellationToken, params string[] path)
            where TResult : IJsonDeserializable
        {
            using Request request = CreateRequest(method, path);
            Response response = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            return CreateResponse(response, resultFactory());
        }

        public Response<TResult> SendRequest<TResult>(RequestMethod method, Func<TResult> resultFactory, CancellationToken cancellationToken, params string[] path)
            where TResult : IJsonDeserializable
        {
            using Request request = CreateRequest(method, path);
            Response response = SendRequest(request, cancellationToken);

            return CreateResponse(response, resultFactory());
        }
        public async Task<Response> SendRequestAsync(RequestMethod method, CancellationToken cancellationToken, params string[] path)
        {
            using Request request = CreateRequest(method, path);
            return await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public Response SendRequest(RequestMethod method, CancellationToken cancellationToken, params string[] path)
        {
            using Request request = CreateRequest(method, path);
            return SendRequest(request, cancellationToken);
        }

        private async Task<Response> SendRequestAsync(Request request, CancellationToken cancellationToken)
        {
            Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

            switch (response.Status)
            {
                case 200:
                case 201:
                case 202:
                case 204:
                    return response;
                default:
                    throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
            }
        }
        private Response SendRequest(Request request, CancellationToken cancellationToken)
        {
            Response response = _pipeline.SendRequest(request, cancellationToken);

            switch (response.Status)
            {
                case 200:
                case 201:
                case 202:
                case 204:
                    return response;
                default:
                    throw response.CreateRequestFailedException();
            }
        }
    }
}
