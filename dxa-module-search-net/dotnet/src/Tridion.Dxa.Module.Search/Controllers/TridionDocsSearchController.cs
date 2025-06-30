using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Sdl.Web.Common.Logging;
using Sdl.Web.Modules.Search.Data;
using Sdl.Web.Mvc.Controllers;
using Sdl.Web.Tridion.ApiClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tridion.Dxa.Api.Client;
using Tridion.Dxa.Api.Client.ContentModel;
using Tridion.Dxa.Api.Client.IqQuery.API;
using Tridion.Dxa.Api.Client.IqQuery.Model.Field;
using Tridion.Dxa.Api.Client.IqQuery.Model.Search;

namespace Sdl.Web.Modules.Search.Controllers
{
    public class TridionDocsSearchController : BaseController
    {
        private static readonly string DEFAULT_SEPARATOR = "+"; // used to be .
        private static readonly string DEFAULT_LANGUAGE = "english";
        private static readonly string PUBLICATION_ONLINE_STATUS_VALUE = "VDITADLVRREMOTESTATUSONLINE";
        private static readonly Regex RegexpDoubleQuotes = new Regex("^\"(.*)\"$", RegexOptions.Compiled);
        private static readonly HashSet<string> Cjk = new HashSet<string> { "chinese", "japanese", "korean" };
        private readonly string _separator = DEFAULT_SEPARATOR;
        private readonly string _namespace;
        private readonly string _defaultLanguage = DEFAULT_LANGUAGE;
        //private readonly bool _useIqService = false;
        private string PublicationOnlineStatusField => $"dynamicText{_separator}FISHDITADLVRREMOTESTATUS.lng.element";
        private string ContentField(string language) => $"content{_separator}{language}";
        private readonly IApiClientFactory _apiClientFactory;

        public TridionDocsSearchController(IApiClientFactory apiClientFactory)
        {
            try
            {
                _apiClientFactory = apiClientFactory;
                _separator = DEFAULT_SEPARATOR;// WebConfigurationManager.AppSettings["iq-field-separator"] ?? DEFAULT_SEPARATOR;
                _defaultLanguage = DEFAULT_LANGUAGE; // WebConfigurationManager.AppSettings["iq-default-language"] ?? DEFAULT_LANGUAGE;
                // if iq-namespace not specified in configuration it will be null and namespace will not be included in iq query (old behavior)
                _namespace = null; // WebConfigurationManager.AppSettings["iq-namespace"];
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        [Route("~/search/{searchQuery}")]
        [HttpGet]
        // We reply here with 204 indicating that the server has already fulfilled the request and there
        // is no additional content. We can't rerun the query at this point because of how the react UI
        // request is made and we lose all context of the search such as language, etc.
        public IActionResult Search(string searchQuery)
        {
            // Your search logic here...

            // Return a No Content (204) status code
            return NoContent();
        }

        [Route("~/api/search")]
        [HttpPost]
        public async virtual Task<IActionResult> Search()
        {
            try
            {
                using var streamReader = new StreamReader(Request.Body);
                var json = await streamReader.ReadToEndAsync();

                var searchParams = JsonConvert.DeserializeObject<SearchParameters>(json);
                var lang = GetLanguage(searchParams);
                ICriteria criteria = null;
              
                var queryString = GetSearchQueryString(searchParams);
                var pubId = GetPublicationId(searchParams);
               
                if (Cjk.Contains(lang))
                {
                    if (pubId == null && string.IsNullOrEmpty(_namespace))
                    {
                        criteria = CompileMultiLangQuery(lang, queryString);
                    }
                    else
                    {
                        if (pubId != null && !string.IsNullOrEmpty(_namespace))
                        {
                            criteria = CompileMultiLangQuery("publicationId", pubId, "namespace", _namespace, lang, queryString);
                        }
                        else
                        {
                            if (pubId != null)
                                criteria = CompileMultiLangQuery("publicationId", pubId, lang,
                                    queryString);
                            else if (!string.IsNullOrEmpty(_namespace))
                                criteria = CompileMultiLangQuery("namespace", _namespace, lang,
                                    queryString);
                        }
                    }
                }
                else
                {
                    var fields = new List<string> { PublicationOnlineStatusField };
                    var values = new List<object> { new DefaultTermValue(PUBLICATION_ONLINE_STATUS_VALUE) };
                    if (pubId != null)
                    {
                        fields.Add("publicationId");
                        values.Add(new DefaultTermValue(pubId));
                    }
                    if (_namespace != null)
                    {
                        fields.Add("namespace");
                        values.Add(new DefaultTermValue(_namespace));
                    }
                    
                    fields.Add(ContentField(GetLanguage(searchParams)));
                    values.Add(new DefaultTermValue(GetSearchQueryString(searchParams)));
                    criteria = new SearchQuery().GroupedAnd(fields, values).Compile();
                }
               
                // Uses graphql search api
                var client = _apiClientFactory.CreateClient();
                var after = searchParams.StartIndex.HasValue ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{searchParams.StartIndex.Value}")) : null;
                var results = client.SearchByRawCriteria(criteria.RawQuery, new InputResultFilter { HighlightingIsEnabled = true },
                    new Pagination
                    {
                        First = searchParams.Count ?? -1,
                        After = after
                    });
                        
                var resultSet = BuildResultSet(results);
                resultSet.Count = searchParams.Count.Value;
                resultSet.StartIndex = searchParams.StartIndex.Value;
                return Json(resultSet);

            }
            catch (Exception e)
            {
                Log.Debug("Failed to execute search", e);
                Response.StatusCode = 405;
                return new EmptyResult();
            }
        }

        private static SearchResultSet BuildResultSet(FacetedSearchResults facetedSearchResults)
        {
            SearchResultSet searchResultSet = new SearchResultSet();
            searchResultSet.QueryResults = new List<SearchResult>();
            if (facetedSearchResults == null || facetedSearchResults.Results == null || facetedSearchResults.Results.Edges == null)
            {
                searchResultSet.Count = 0;
                searchResultSet.Hits = 0;
                searchResultSet.StartIndex = 0;
                return searchResultSet;
            }

            searchResultSet.Hits = facetedSearchResults.Results.Hits ?? 0;

            foreach (var result in facetedSearchResults.Results.Edges)
            {
                SearchResult searchResult = new SearchResult
                {
                    Id = result.Node.Search.Id,
                    Content = result.Node.Search.MainContentField,
                    Language = result.Node.Search.Locale,
                    LastModifiedDate = result.Node.Search.ModifiedDate,
                    PublicationId = result.Node.Search.PublicationId ?? 0,
                    PublicationTitle = result.Node.Search.PublicationTitle,
                    Meta = result.Node.Search.Fields,
                    Highlighted = result.Node.Search.Highlighted,
                    ItemType = (result.Node.Search.ItemType == ItemType.Binary)? ItemType.Binary : ItemType.Page
                };

                searchResultSet.QueryResults.Add(searchResult);

            }
            return searchResultSet;
        }

        private ICriteria CompileMultiLangQuery(string language, string queryString) =>
            new SearchQuery()
                .Field(PublicationOnlineStatusField, PUBLICATION_ONLINE_STATUS_VALUE)
                .And()
                .GroupStart()
                .Field(ContentField("cjk"), queryString)
                .Or()
                .Field(ContentField(language), queryString)
                .GroupEnd()
                .Compile();

        private ICriteria CompileMultiLangQuery(string fieldName, object fieldValue, string language, string queryString) =>
            new SearchQuery()
                .GroupStart()
                .Field(PublicationOnlineStatusField, PUBLICATION_ONLINE_STATUS_VALUE)
                .And()
                .Field(fieldName, new DefaultTermValue(fieldValue))
                .GroupEnd()
                .And()
                .GroupStart()
                .Field(ContentField("cjk"), queryString)
                .Or()
                .Field(ContentField(language), queryString)
                .GroupEnd()
                .Compile();

        private ICriteria CompileMultiLangQuery(string fieldName1, object fieldValue1, string fieldName2, object fieldValue2, string language, string queryString) =>
            new SearchQuery()
                .GroupStart()
                .Field(PublicationOnlineStatusField, PUBLICATION_ONLINE_STATUS_VALUE)
                .And()
                .Field(fieldName1, new DefaultTermValue(fieldValue1))
                .GroupEnd()
                .And()
                .GroupStart()
                .Field(fieldName2, new DefaultTermValue(fieldValue2))
                .And()
                .GroupStart()
                .Field(ContentField("cjk"), queryString)
                .Or()
                .Field(ContentField(language), queryString)
                .GroupEnd()
                .GroupEnd()
                .Compile();

        private static string GetPublicationId(SearchParameters searchParameters) 
            => searchParameters.PublicationId?.ToString();

        private string GetLanguage(SearchParameters searchParameters) => string.IsNullOrEmpty(searchParameters.Language) ? _defaultLanguage : CultureInfo.GetCultureInfo(searchParameters.Language.Split('-')[0]).EnglishName.ToLower();

        private static string GetSearchQueryString(SearchParameters searchParameters)
        {
            // perform escaping if required
            string searchQuery = searchParameters.SearchQuery;
            Match match = RegexpDoubleQuotes.Match(searchQuery);
            if (match.Success)
            {
                searchQuery = match.Groups[1].Value;
            }
            else
            {
                searchQuery = searchQuery.Replace("\"", "");
            }
            return searchQuery;
        }
    }
}
