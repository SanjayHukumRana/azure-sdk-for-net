﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Http;
using Azure.Core.Pipeline;

namespace Azure.Data.AppConfiguration
{
    /// <summary>
    /// The client to use for interacting with the Azure Configuration Store.
    /// </summary>
    public partial class ConfigurationClient
    {
        private readonly Uri _baseUri;
        private readonly HttpPipeline _pipeline;

        /// <summary>
        /// Protected constructor to allow mocking
        /// </summary>
        protected ConfigurationClient()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationClient"/>.
        /// </summary>
        /// <param name="connectionString">Connection string with authentication option and related parameters.</param>
        public ConfigurationClient(string connectionString)
            : this(connectionString, new ConfigurationClientOptions())
        {
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationClient"/> that sends requests to the configuration store.
        /// </summary>
        /// <param name="connectionString">Connection string with authentication option and related parameters.</param>
        /// <param name="options">Options that allow to configure the management of the request sent to the configuration store.</param>
        public ConfigurationClient(string connectionString, ConfigurationClientOptions options)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            ParseConnectionString(connectionString, out _baseUri, out var credential, out var secret);

            _pipeline = HttpPipelineBuilder.Build(options,
                    new HttpPipelinePolicy[] { new CustomHeadersPolicy() },
                    new HttpPipelinePolicy[] {
                        new ApiVersionPolicy(options.GetVersionString()),
                        new AuthenticationPolicy(credential, secret),
                        new SyncTokenPolicy() },
                    new ResponseClassifier());
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> only if the setting does not already exist in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="value">The value of the configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> AddAsync(string key, string value, string label = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");
            return await AddAsync(new ConfigurationSetting(key, value, label), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> only if the setting does not already exist in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="value">The value of the configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> Add(string key, string value, string label = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");
            return Add(new ConfigurationSetting(key, value, label), cancellationToken);
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> only if the setting does not already exist in the configuration store.
        /// </summary>
        /// <param name="setting"><see cref="ConfigurationSetting"/> to create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> AddAsync(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Add");
            scope.Start();

            try
            {
                using Request request = CreateAddRequest(setting);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                    case 201:
                        return await CreateResponseAsync(response, cancellationToken).ConfigureAwait(false);
                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> only if the setting does not already exist in the configuration store.
        /// </summary>
        /// <param name="setting"><see cref="ConfigurationSetting"/> to create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> Add(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Add");
            scope.AddAttribute("key", setting?.Key);
            scope.Start();

            try
            {
                using Request request = CreateAddRequest(setting);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)
                {
                    case 200:
                    case 201:
                        return CreateResponse(response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateAddRequest(ConfigurationSetting setting)
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));
            if (string.IsNullOrEmpty(setting.Key))
                throw new ArgumentNullException($"{nameof(setting)}.{nameof(setting.Key)}");

            Request request = _pipeline.CreateRequest();

            ReadOnlyMemory<byte> content = Serialize(setting);

            request.Method = RequestMethod.Put;

            BuildUriForKvRoute(request.Uri, setting);

            request.Headers.Add(IfNoneMatch, "*");
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);
            request.Headers.Add(HttpHeader.Common.JsonContentType);
            request.Content = HttpPipelineRequestContent.Create(content);

            return request;
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> if it doesn't exist or overrides an existing setting in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="value">The value of the configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> SetAsync(string key, string value, string label = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");
            return await SetAsync(new ConfigurationSetting(key, value, label), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> if it doesn't exist or overrides an existing setting in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="value">The value of the configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> Set(string key, string value, string label = default, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");
            return Set(new ConfigurationSetting(key, value, label), cancellationToken);
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> if it doesn't exist or overrides an existing setting in the configuration store.
        /// </summary>
        /// <param name="setting"><see cref="ConfigurationSetting"/> to create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> SetAsync(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Set");
            scope.AddAttribute("key", setting?.Key);
            scope.Start();

            try
            {
                using Request request = CreateSetRequest(setting);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                return response.Status switch
                {
                    200 => await CreateResponseAsync(response, cancellationToken).ConfigureAwait(false),
                    409 => throw await response.CreateRequestFailedExceptionAsync("The setting is locked").ConfigureAwait(false),
                    _ => throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false),
                };
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="ConfigurationSetting"/> if it doesn't exist or overrides an existing setting in the configuration store.
        /// </summary>
        /// <param name="setting"><see cref="ConfigurationSetting"/> to create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> Set(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Set");
            scope.AddAttribute("key", setting?.Key);
            scope.Start();

            try
            {
                using Request request = CreateSetRequest(setting);

                Response response = _pipeline.SendRequest(request, cancellationToken);

                return response.Status switch
                {
                    200 => CreateResponse(response),
                    409 => throw response.CreateRequestFailedException("The setting is locked"),
                    _ => throw response.CreateRequestFailedException(),
                };
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateSetRequest(ConfigurationSetting setting)
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));
            if (string.IsNullOrEmpty(setting.Key))
                throw new ArgumentNullException($"{nameof(setting)}.{nameof(setting.Key)}");

            Request request = _pipeline.CreateRequest();
            ReadOnlyMemory<byte> content = Serialize(setting);

            request.Method = RequestMethod.Put;
            BuildUriForKvRoute(request.Uri, setting);
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);
            request.Headers.Add(HttpHeader.Common.JsonContentType);

            if (setting.ETag != default)
            {
                request.Headers.Add(IfMatchName, $"\"{setting.ETag.ToString()}\"");
            }

            request.Content = HttpPipelineRequestContent.Create(content);
            return request;
        }

        /// <summary>
        /// Deletes an existing <see cref="ConfigurationSetting"/> in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only deleted if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always deleted.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response> DeleteAsync(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Delete");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateDeleteRequest(key, label, etag);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                    case 204:
                        return response;
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Deletes an existing <see cref="ConfigurationSetting"/> in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only deleted if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always deleted.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response Delete(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Delete");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateDeleteRequest(key, label, etag);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)
                {
                    case 200:
                    case 204:
                        return response;
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateDeleteRequest(string key, string label, ETag etag)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Delete;
            BuildUriForKvRoute(request.Uri, key, label);

            if (etag != default)
            {
                request.Headers.Add(IfMatchName, $"\"{etag.ToString()}\"");
            }

            return request;
        }

        /// <summary>
        /// Retrieve an existing <see cref="ConfigurationSetting"/> from the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="acceptDateTime">The setting will be retrieved exactly as it existed at the provided time.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> GetAsync(string key, string label = default, DateTimeOffset acceptDateTime = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Get");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateGetRequest(key, label, acceptDateTime);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                return response.Status switch
                {
                    200 => await CreateResponseAsync(response, cancellationToken).ConfigureAwait(false),
                    _ => throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false),
                };
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Retrieve an existing <see cref="ConfigurationSetting"/> from the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="acceptDateTime">The setting will be retrieved exactly as it existed at the provided time.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> Get(string key, string label = default, DateTimeOffset acceptDateTime = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.Get");
            scope.AddAttribute(nameof(key), key);
            scope.Start();

            try
            {
                using (Request request = CreateGetRequest(key, label, acceptDateTime))
                {
                    Response response = _pipeline.SendRequest(request, cancellationToken);

                    return response.Status switch
                    {
                        200 => CreateResponse(response),
                        _ => throw response.CreateRequestFailedException(),
                    };
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Retrieves one or more <see cref="ConfigurationSetting"/> that satisfies the options of the <see cref="SettingSelector"/>
        /// </summary>
        /// <param name="selector">Set of options for selecting <see cref="ConfigurationSetting"/> from the configuration store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual AsyncPageable<ConfigurationSetting> GetSettingsAsync(SettingSelector selector, CancellationToken cancellationToken = default)
        {
            return PageResponseEnumerator.CreateAsyncEnumerable(nextLink => GetSettingsPageAsync(selector, nextLink, cancellationToken));
        }

        /// <summary>
        /// Retrieves one or more <see cref="ConfigurationSetting"/> that satisfies the options of the <see cref="SettingSelector"/>
        /// </summary>
        /// <param name="selector">Set of options for selecting <see cref="ConfigurationSetting"/> from the configuration store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Pageable<ConfigurationSetting> GetSettings(SettingSelector selector, CancellationToken cancellationToken = default)
        {
            return PageResponseEnumerator.CreateEnumerable(nextLink => GetSettingsPage(selector, nextLink, cancellationToken));
        }

        /// <summary>
        /// Retrieves the different revisions of specific <see cref="ConfigurationSetting"/> that satisfies the options of the <see cref="SettingSelector"/>
        /// </summary>
        /// <param name="selector">Set of options for selecting <see cref="ConfigurationSetting"/> from the configuration store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual AsyncPageable<ConfigurationSetting> GetRevisionsAsync(SettingSelector selector, CancellationToken cancellationToken = default)
        {
            return PageResponseEnumerator.CreateAsyncEnumerable(nextLink => GetRevisionsPageAsync(selector, nextLink, cancellationToken));
        }

        /// <summary>
        /// Retrieves the different revisions of specific <see cref="ConfigurationSetting"/> that satisfies the options of the <see cref="SettingSelector"/>
        /// </summary>
        /// <param name="selector">Set of options for selecting <see cref="ConfigurationSetting"/> from the configuration store.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Pageable<ConfigurationSetting> GetRevisions(SettingSelector selector, CancellationToken cancellationToken = default)
        {
            return PageResponseEnumerator.CreateEnumerable(nextLink => GetRevisionsPage(selector, nextLink, cancellationToken));
        }

        private Request CreateGetRequest(string key, string label, DateTimeOffset acceptDateTime)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");

            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Get;
            BuildUriForKvRoute(request.Uri, key, label);
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);

            if (acceptDateTime != default)
            {
                var dateTime = acceptDateTime.UtcDateTime.ToString(AcceptDateTimeFormat, CultureInfo.InvariantCulture);
                request.Headers.Add(AcceptDatetimeHeader, dateTime);
            }

            request.Headers.Add(HttpHeader.Common.JsonContentType);
            return request;
        }

        /// <summary>
        /// Fetches the <see cref="ConfigurationSetting"/> from the configuration store that match the options selected in the <see cref="SettingSelector"/>.
        /// </summary>
        /// <param name="selector">Set of options for selecting settings from the configuration store.</param>
        /// <param name="pageLink"></param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        private async Task<Page<ConfigurationSetting>> GetSettingsPageAsync(SettingSelector selector, string pageLink, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.GetSettingsPage");
            scope.Start();

            try
            {
                using Request request = CreateBatchRequest(selector, pageLink);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                    case 206:
                        SettingBatch settingBatch = await ConfigurationServiceSerializer.ParseBatchAsync(response, cancellationToken).ConfigureAwait(false);
                        return new Page<ConfigurationSetting>(settingBatch.Settings, settingBatch.NextBatchLink, response);
                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Fetches the <see cref="ConfigurationSetting"/> from the configuration store that match the options selected in the <see cref="SettingSelector"/>.
        /// </summary>
        /// <param name="selector">Set of options for selecting settings from the configuration store.</param>
        /// <param name="pageLink"></param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        private Page<ConfigurationSetting> GetSettingsPage(SettingSelector selector, string pageLink, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.GetSettingsPage");
            scope.Start();

            try
            {
                using Request request = CreateBatchRequest(selector, pageLink);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)
                {
                    case 200:
                    case 206:
                        SettingBatch settingBatch = ConfigurationServiceSerializer.ParseBatch(response);
                        return new Page<ConfigurationSetting>(settingBatch.Settings, settingBatch.NextBatchLink, response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateBatchRequest(SettingSelector selector, string pageLink)
        {
            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Get;
            BuildUriForGetBatch(request.Uri, selector, pageLink);
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);
            if (selector.AsOf.HasValue)
            {
                var dateTime = selector.AsOf.Value.UtcDateTime.ToString(AcceptDateTimeFormat, CultureInfo.InvariantCulture);
                request.Headers.Add(AcceptDatetimeHeader, dateTime);
            }

            return request;
        }

        /// <summary>
        /// Lists chronological/historical representation of <see cref="ConfigurationSetting"/> from the configuration store that match the options selected in the <see cref="SettingSelector"/>.
        /// </summary>
        /// <remarks>Revisions are provided in descending order from their respective <see cref="ConfigurationSetting.LastModified"/> date.</remarks>
        /// <param name="selector">Set of options for selecting settings from the configuration store.</param>
        /// <param name="pageLink"></param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        private async Task<Page<ConfigurationSetting>> GetRevisionsPageAsync(SettingSelector selector, string pageLink, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.GetRevisionsPage");
            scope.Start();

            try
            {
                using Request request = CreateGetRevisionsRequest(selector, pageLink);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
                switch (response.Status)
                {
                    case 200:
                    case 206:
                        SettingBatch settingBatch = await ConfigurationServiceSerializer.ParseBatchAsync(response, cancellationToken).ConfigureAwait(false);
                        return new Page<ConfigurationSetting>(settingBatch.Settings, settingBatch.NextBatchLink, response);
                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Lists chronological/historical representation of <see cref="ConfigurationSetting"/> from the configuration store that match the options selected in the <see cref="SettingSelector"/>.
        /// </summary>
        /// <remarks>Revisions are provided in descending order from their respective <see cref="ConfigurationSetting.LastModified"/> date.</remarks>
        /// <param name="selector">Set of options for selecting settings from the configuration store.</param>
        /// <param name="pageLink"></param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        private Page<ConfigurationSetting> GetRevisionsPage(SettingSelector selector, string pageLink, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.GetRevisionsPage");
            scope.Start();

            try
            {
                using Request request = CreateGetRevisionsRequest(selector, pageLink);
                Response response = _pipeline.SendRequest(request, cancellationToken);
                switch (response.Status)
                {
                    case 200:
                    case 206:
                        SettingBatch settingBatch = ConfigurationServiceSerializer.ParseBatch(response);
                        return new Page<ConfigurationSetting>(settingBatch.Settings, settingBatch.NextBatchLink, response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateGetRevisionsRequest(SettingSelector selector, string pageLink)
        {
            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Get;
            BuildUriForRevisions(request.Uri, selector, pageLink);
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);
            if (selector.AsOf.HasValue)
            {
                var dateTime = selector.AsOf.Value.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);
                request.Headers.Add(AcceptDatetimeHeader, dateTime);
            }

            return request;
        }

        /// <summary>
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task<Response<bool>> HasChangedAsync(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.HasChanged");
            scope.AddAttribute("key", setting.Key);
            scope.Start();

            try
            {
                using Request request = CreateHeadRequest(setting.Key, setting.Label);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                    case 404:
                        string etag = default;
                        if (response.Headers.TryGetValue(ETag, out etag))
                        {
                            etag = etag.Trim('\"');
                        }

                        return Response.FromValue(response, setting.ETag != new ETag(etag));

                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                };
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Response<bool> HasChanged(ConfigurationSetting setting, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.HasChanged");
            scope.AddAttribute(nameof(setting.Key), setting.Key);
            scope.Start();

            try
            {
                using (Request request = CreateHeadRequest(setting.Key, setting.Label))
                {
                    Response response = _pipeline.SendRequest(request, cancellationToken);

                    switch (response.Status)
                    {
                        case 200:
                        case 404:
                            string etag = default;
                            if (response.Headers.TryGetValue(ETag, out etag))
                            {
                                etag = etag.Trim('\"');
                            }

                            return Response.FromValue(response, setting.ETag != new ETag(etag));

                        default:
                            throw response.CreateRequestFailedException();
                    }
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateHeadRequest(string key, string label)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException($"{nameof(key)}");

            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Head;
            BuildUriForKvRoute(request.Uri, key, label);
            request.Headers.Add(s_mediaTypeKeyValueApplicationHeader);
            request.Headers.Add(HttpHeader.Common.JsonContentType);
            return request;
        }

        /// <summary>
        /// Sets an existing <see cref="ConfigurationSetting"/> as read only in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only set to read only if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always set to read only.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> SetReadOnlyAsync(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.SetReadOnly");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateSetReadOnlyRequest(key, label, etag);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                        return CreateResponse(response);
                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Sets an existing <see cref="ConfigurationSetting"/> as read only in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only set to read only if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always set to read only.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> SetReadOnly(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.SetReadOnly");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateSetReadOnlyRequest(key, label, etag);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)
                {
                    case 200:
                        return CreateResponse(response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateSetReadOnlyRequest(string key, string label, ETag etag)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Put;
            BuildUriForLocksRoute(request.Uri, key, label);

            if (etag != default)
            {
                request.Headers.Add(IfMatchName, $"\"{etag.ToString()}\"");
            }

            return request;
        }

        /// <summary>
        /// Sets an existing <see cref="ConfigurationSetting"/> as read write in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only set to read write if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always set to read write.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual async Task<Response<ConfigurationSetting>> ClearReadOnlyAsync(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.ClearReadOnly");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateClearReadOnlyRequest(key, label, etag);
                Response response = await _pipeline.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.Status)
                {
                    case 200:
                        return CreateResponse(response);
                    default:
                        throw await response.CreateRequestFailedExceptionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        /// Sets an existing <see cref="ConfigurationSetting"/> as read write in the configuration store.
        /// </summary>
        /// <param name="key">The primary identifier of a configuration setting.</param>
        /// <param name="label">The value used to group configuration settings.</param>
        /// <param name="etag">The value of an etag indicates the state of a configuration setting within a configuration store.
        /// If it is specified, the configuration setting is only set to read write if etag value matches etag value in the configuration store.
        /// If no etag value is passed in, then the setting is always set to read write.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> controlling the request lifetime.</param>
        public virtual Response<ConfigurationSetting> ClearReadOnly(string key, string label = default, ETag etag = default, CancellationToken cancellationToken = default)
        {
            using DiagnosticScope scope = _pipeline.Diagnostics.CreateScope("Azure.Data.AppConfiguration.ConfigurationClient.ClearReadOnly");
            scope.AddAttribute("key", key);
            scope.Start();

            try
            {
                using Request request = CreateClearReadOnlyRequest(key, label, etag);
                Response response = _pipeline.SendRequest(request, cancellationToken);

                switch (response.Status)
                {
                    case 200:
                        return CreateResponse(response);
                    default:
                        throw response.CreateRequestFailedException();
                }
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        private Request CreateClearReadOnlyRequest(string key, string label, ETag etag)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            Request request = _pipeline.CreateRequest();
            request.Method = RequestMethod.Delete;
            BuildUriForLocksRoute(request.Uri, key, label);

            if (etag != default)
            {
                request.Headers.Add(IfMatchName, $"\"{etag.ToString()}\"");
            }

            return request;
        }
    }
}
