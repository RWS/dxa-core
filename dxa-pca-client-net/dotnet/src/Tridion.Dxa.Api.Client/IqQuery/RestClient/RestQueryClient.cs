﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tridion.Dxa.Api.Client.HttpClient;
using Tridion.Dxa.Api.Client.HttpClient.Auth;
using Tridion.Dxa.Api.Client.HttpClient.Request;
using Tridion.Dxa.Api.Client.HttpClient.Response;
using Tridion.Dxa.Api.Client.IqQuery.API;
using Tridion.Dxa.Api.Client.IqQuery.Model;

namespace Tridion.Dxa.Api.Client.IqQuery.RestClient
{
    public class RestQueryClient<T, R> : IQueryClient<T, R> where T : IQueryResultData<R> where R : IQueryResult
    {
        private readonly IHttpClient _client;
        private readonly IAuthentication _auth;
        private readonly string _defautIndexName;

        public RestQueryClient(Uri endpoint, IAuthentication auth, string defaultIndexName = "udp-index")
        {
            _client = new HttpClient.HttpClient(endpoint);
            _defautIndexName = defaultIndexName;
            _auth = auth;
        }

        public virtual T SearchById(string index, string id)
        {
            IHttpClientRequest request = CreateRequest(index, "GET", null, null);
            request.Path = $"/v1/search/{index}/{id}";
            IHttpClientResponse<T> response = _client.Execute<T>(request);
            return response.ResponseData;
        }

        public virtual async Task<T> SearchByIdAsync(string index, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            IHttpClientRequest request = CreateRequest(index, "GET", null, null);
            request.Path = $"/v1/search/{index}/{id}";
            IHttpClientResponse<T> response = await _client.ExecuteAsync<T>(request, cancellationToken);
            return response.ResponseData;
        }

        public virtual T SearchWithCriteria(string index, string criteria, IResultFilter filter)
        {
            IHttpClientRequest request = CreateRequest(index, "POST", criteria, filter);
            IHttpClientResponse<T> response = _client.Execute<T>(request);
            return response.ResponseData;
        }

        public virtual async Task<T> SearchWithCriteriaAsync(string index, string criteria, IResultFilter filter, CancellationToken cancellationToken = default(CancellationToken))
        {
            IHttpClientRequest request = CreateRequest(index, "POST", criteria, filter);
            IHttpClientResponse<T> response = await _client.ExecuteAsync<T>(request, cancellationToken).ConfigureAwait(false);
            return response.ResponseData;
        }

        public T SearchWithCriteria(string index, ICriteria criteria, IResultFilter filter)
        {
            IHttpClientRequest request = CreateRequest(index, "POST", criteria.RawQuery, filter);
            IHttpClientResponse<T> response = _client.Execute<T>(request);
            return response.ResponseData;
        }

        public virtual async Task<T> SearchWithCriteriaAsync(string index, ICriteria criteria, IResultFilter filter, CancellationToken cancellationToken = default(CancellationToken))
        {
            IHttpClientRequest request = CreateRequest(index, "POST", criteria.RawQuery, filter);
            IHttpClientResponse<T> response = await _client.ExecuteAsync<T>(request, cancellationToken).ConfigureAwait(false);
            return response.ResponseData;
        }

        protected virtual IHttpClientRequest CreateRequest(string indexName, string method, object criteria, IResultFilter filter)
        {
            IHttpClientRequest request = new HttpClientRequest
            {
                Path = $"/v1/search/{indexName ?? _defautIndexName}",
                ContentType = "application/json",
                Method = method,
                Authentication = _auth,
                Body = criteria
            };

            return CreateQueryParameters(request, filter, null); //TODO qtName
        }

        protected virtual IHttpClientRequest CreateQueryParameters(IHttpClientRequest request, IResultFilter filter, string qtName)
        {
            if (qtName != null)
            {
                request.QueryParameters.Add(new KeyValuePair<string, object>(QueryConstants.ResultModel, qtName));
            }

            if (filter == null) return request;

            if (filter.ExcludeFields != null && filter.ExcludeFields.Count > 0)
            {
                request.QueryParameters.Add(QueryConstants.ExcludeFields, string.Join(",", filter.ExcludeFields.Select(x => x)));
            }

            if (filter.StartOfRange.HasValue)
            {
                request.QueryParameters.Add(QueryConstants.StartRange, filter.StartOfRange.Value);
            }

            if (filter.EndOfRange.HasValue)
            {
                request.QueryParameters.Add(QueryConstants.EndRange, filter.EndOfRange.Value);
            }

            if (filter.MaxResults.HasValue)
            {
                request.QueryParameters.Add(QueryConstants.MaxResults, filter.MaxResults.Value);
            }

            request.QueryParameters.Add(QueryConstants.Highlighting, filter.IsHighlightingEnabled);
            request.QueryParameters.Add(QueryConstants.HighlightInAll, filter.IsHighlightInAllEnabled);
            return request;
        }
    }
}
