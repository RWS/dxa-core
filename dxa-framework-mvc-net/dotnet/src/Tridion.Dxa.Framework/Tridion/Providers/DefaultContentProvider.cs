using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.DataModel;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Tridion.ApiClient;
using Sdl.Web.Tridion.Providers.Query;
using Sdl.Web.Tridion.Statics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tridion.Dxa.Api.Client;
using Tridion.Dxa.Api.Client.ContentModel;
using Tridion.Dxa.Framework.Common.Utils;

namespace Sdl.Web.Tridion.Mapping
{
    /// <summary>
    /// Default Content Provider implementation (based on DXA R2 Data Model).
    /// </summary>
    public class DefaultContentProvider : IContentProviderExt, IRawDataProvider
    {
        private readonly IApiClientFactory _apiClientFactory;
        private readonly ICursorIndexerService _cursorIndexerService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DefaultContentProvider(
            IApiClientFactory apiClientFactory)
        {
            _apiClientFactory = apiClientFactory;
            ModelBuilderPipeline.Init();
        }

        public DefaultContentProvider(
            IApiClientFactory apiClientFactory,
            ICursorIndexerService cursorIndexerService,
            IHttpContextAccessor httpContextAccessor)
        {
            _apiClientFactory = apiClientFactory;
            _cursorIndexerService = cursorIndexerService;
            _httpContextAccessor = httpContextAccessor;
            ModelBuilderPipeline.Init();
        }

        /// <summary>
        /// Gets a Page Model for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path (unescaped).</param>
        /// <param name="localization">The context <see cref="ILocalization"/>.</param>
        /// <param name="addIncludes">Indicates whether include Pages should be expanded.</param>
        /// <returns>The Page Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Page Model exists for the given URL.</exception>
        public virtual PageModel GetPageModel(string urlPath, Localization localization, bool addIncludes = true)
        {
            using (new Tracer(urlPath, localization, addIncludes))
            {
                PageModel result = null;
                if (CacheRegions.IsViewModelCachingEnabled)
                {
                    PageModel cachedPageModel = SiteConfiguration.CacheProvider.GetOrAdd(
                        $"{localization.Id}:{urlPath}:{addIncludes}:{WebRequestContext.Current.CacheKeySalt}", // Cache Page Models with and without includes separately
                        CacheRegions.PageModel,
                        () =>
                        {
                            PageModel pageModel = LoadPageModel(ref urlPath, addIncludes, localization);
                            if (pageModel.NoCache || pageModel.HasNoCacheAttribute)
                            {
                                result = pageModel;
                                pageModel.IsVolatile = true;
                                return null;
                            }
                            return pageModel;
                        }
                        );

                    if (cachedPageModel != null)
                    {
                        // Don't return the cached Page Model itself, because we don't want dynamic logic to modify the cached state.
                        result = (PageModel)cachedPageModel.DeepCopy();
                    }
                }
                else
                {
                    result = LoadPageModel(ref urlPath, addIncludes, localization);
                }

                if (SiteConfiguration.ConditionalEntityEvaluator != null)
                {
                    result.FilterConditionalEntities(localization);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets a Page Model for a given Page Id.
        /// </summary>
        /// <param name="pageId">Page Id</param>
        /// <param name="localization">The context Localization.</param>
        /// <param name="addIncludes">Indicates whether include Pages should be expanded.</param>
        /// <returns>The Page Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Page Model exists for the given Id.</exception>
        public virtual PageModel GetPageModel(int pageId, Localization localization, bool addIncludes = true)
        {
            using (new Tracer(localization.Id, pageId, localization, addIncludes))
            {
                PageModel result = null;
                if (CacheRegions.IsViewModelCachingEnabled)
                {
                    PageModel cachedPageModel = SiteConfiguration.CacheProvider.GetOrAdd(
                        $"{localization.Id}-{pageId}:{addIncludes}:{WebRequestContext.Current.CacheKeySalt}", // Cache Page Models with and without includes separately
                        CacheRegions.PageModel,
                        () =>
                        {
                            PageModel pageModel = LoadPageModel(pageId, addIncludes, localization);
                            if (pageModel.NoCache || pageModel.HasNoCacheAttribute)
                            {
                                result = pageModel;
                                pageModel.IsVolatile = true;
                                return null;
                            }
                            return pageModel;
                        }
                        );

                    if (cachedPageModel != null)
                    {
                        // Don't return the cached Page Model itself, because we don't want dynamic logic to modify the cached state.
                        result = (PageModel)cachedPageModel.DeepCopy();
                    }
                }
                else
                {
                    result = LoadPageModel(pageId, addIncludes, localization);
                }

                if (SiteConfiguration.ConditionalEntityEvaluator != null)
                {
                    result.FilterConditionalEntities(localization);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets an Entity Model for a given Entity Identifier.
        /// </summary>
        /// <param name="id">The Entity Identifier. Must be in format {ComponentID}-{TemplateID}.</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Entity Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Entity Model exists for the given URL.</exception>
        public virtual EntityModel GetEntityModel(string id, Localization localization)
        {
            using (new Tracer(id, localization))
            {
                EntityModel result = null;
                if (CacheRegions.IsViewModelCachingEnabled)
                {
                    EntityModel cachedEntityModel = SiteConfiguration.CacheProvider.GetOrAdd(
                        $"{id}-{localization.Id}:{WebRequestContext.Current.CacheKeySalt}", // key
                        CacheRegions.EntityModel,
                        () =>
                        {
                            EntityModel entityModel = LoadEntityModel(id, localization);
                            if (entityModel.HasNoCacheAttribute)
                            {
                                // this entity has been marked for no caching so we return null to prevent a cache write                         
                                entityModel.IsVolatile = true;
                                result = entityModel;
                                return null;
                            }

                            return entityModel;
                        }
                    );

                    if (cachedEntityModel != null)
                    {
                        // Don't return the cached Entity Model itself, because we don't want dynamic logic to modify the cached state.
                        result = (EntityModel)cachedEntityModel.DeepCopy();
                    }
                }
                else
                {
                    result = LoadEntityModel(id, localization);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets a Static Content Item for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path (unescaped).</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Static Content Item.</returns>
        public virtual StaticContentItem GetStaticContentItem(string urlPath, Localization localization)
        {
            using (new Tracer(urlPath, localization))
            {
                if (WebRequestContext.Current.IsSessionPreview)
                {
                    // If running under an XPM session preview go directly to BinaryProvider and avoid any
                    // caching logic provided by the BinaryFileManager. We still need to perform image
                    // resizing due to responsive image urls.
                    BinaryFileManager.Dimensions dims;
                    urlPath = BinaryFileManager.StripDimensions(urlPath, out dims);
                    var binary = BinaryFileManager.Provider.GetBinary(localization, urlPath);
                    byte[] binaryData = binary.Item1;
                    if (dims != null && (dims.Width > 0 || dims.Height > 0))
                    {
                        string imgFormat = BinaryFileManager.GetImageFormat(binary.Item2);
                        if (imgFormat != null) binaryData = BinaryFileManager.ResizeImage(binaryData, dims, imgFormat);
                    }

                    return new StaticContentItem(
                        new MemoryStream(binaryData),
                        MimeMapping.GetMimeMapping(binary.Item2),
                        DateTime.Now,
                        Encoding.UTF8);
                }

                MemoryStream memoryStream;
                Stream dataStream;
                string localFilePath =
                    BinaryFileManager.Instance.GetCachedFile(urlPath, localization, out memoryStream);

                if (memoryStream == null)
                {
                    dataStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                        FileOptions.SequentialScan);
                }
                else
                {
                    dataStream = memoryStream;
                }

                return new StaticContentItem(
                    dataStream,
                    MimeMapping.GetMimeMapping(localFilePath),
                    File.GetLastWriteTime(localFilePath),
                    Encoding.UTF8
                );
            }
        }

        /// <summary>
        /// Gets a Static Content Item for a given Id.
        /// </summary>
        /// <param name="binaryId">The Id of the binary.</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Static Content Item.</returns>
        public virtual StaticContentItem GetStaticContentItem(int binaryId, Localization localization)
        {
            using (new Tracer(binaryId, localization))
            {
                // If running under an XPM session preview go directly to BinaryProvider and avoid any
                // caching logic provided by the BinaryFileManager.
                if (WebRequestContext.Current.IsSessionPreview)
                {
                    var binary = BinaryFileManager.Provider.GetBinary(localization, binaryId);
                    return new StaticContentItem(
                        new MemoryStream(binary.Item1),
                        MimeMapping.GetMimeMapping(binary.Item2),
                        DateTime.Now,
                        Encoding.UTF8);
                }

                MemoryStream memoryStream;
                Stream dataStream;
                string localFilePath = BinaryFileManager.Instance.GetCachedFile(binaryId, localization, out memoryStream);

                if (memoryStream == null)
                {
                    dataStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                        FileOptions.SequentialScan);
                }
                else
                {
                    dataStream = memoryStream;
                }

                return new StaticContentItem(
                    dataStream,
                    MimeMapping.GetMimeMapping(localFilePath),
                    File.GetLastWriteTime(localFilePath),
                    Encoding.UTF8
                    );
            }
        }

        /// <summary>
        /// Populates a Dynamic List by executing the query it specifies.
        /// </summary>
        /// <param name="dynamicList">The Dynamic List which specifies the query and is to be populated.</param>
        /// <param name="localization">The context Localization.</param>
        public virtual void PopulateDynamicList(DynamicList dynamicList, Localization localization)
        {
            using (new Tracer(dynamicList, localization))
            {
                SimpleBrokerQuery simpleBrokerQuery = dynamicList.GetQuery(localization) as SimpleBrokerQuery;
                if (simpleBrokerQuery == null)
                {
                    throw new DxaException($"Unexpected result from {dynamicList.GetType().Name}.GetQuery: {dynamicList.GetQuery(localization)}");
                }

                // get our cursor indexer for this list
                var cursors = _cursorIndexerService.GetCursorIndexer(dynamicList.Id);

                // given our start index into the paged list we need to translate that to a cursor
                int start = simpleBrokerQuery.Start;
                simpleBrokerQuery.Cursor = cursors[start];

                // the cursor retrieved may of came from a different start index so we update start
                int startIndex = cursors.StartIndex;
                simpleBrokerQuery.Start = startIndex;
                dynamicList.Start = startIndex;

                var cachedDynamicList = SiteConfiguration.CacheProvider.GetOrAdd(
                    $"PopulateDynamicList-{dynamicList.Id}-{simpleBrokerQuery.GetHashCode()}", // key
                    CacheRegions.BrokerQuery,
                    () =>
                    {
                        var brokerQuery = new GraphQLQueryProvider(_apiClientFactory);

                        var components = brokerQuery.ExecuteQueryItems(simpleBrokerQuery).ToList();
                        Log.Debug($"Broker Query returned {components.Count} results. HasMore={brokerQuery.HasMore}");

                        if (components.Count > 0)
                        {
                            Type resultType = dynamicList.ResultType;
                            dynamicList.QueryResults = components
                                .Select(
                                    c =>
                                        ModelBuilderPipeline.CreateEntityModel(
                                            CreateEntityModelData((Component)c), resultType,
                                            localization))
                                .ToList();
                        }

                        dynamicList.HasMore = brokerQuery.HasMore;

                        if (brokerQuery.HasMore)
                        {
                            // update cursor
                            cursors[simpleBrokerQuery.Start + simpleBrokerQuery.PageSize] = brokerQuery.Cursor;
                        }

                        return dynamicList;
                    });

                dynamicList.QueryResults = cachedDynamicList.QueryResults;
                dynamicList.HasMore = cachedDynamicList.HasMore;
            }
        }

        public virtual string GetPageContent(string urlPath, Localization localization)
        {
            using (new Tracer(urlPath, localization))
            {
                if (!urlPath.EndsWith(Constants.DefaultExtension) && !urlPath.EndsWith(".json"))
                {
                    urlPath += Constants.DefaultExtension;
                }

                var client = _apiClientFactory.CreateClient();
                // Important: The content we are getting back is not model based so we need to inform
                // the PCA so it doesn't attempt to treat it as a R2/DD4T model and attempt conversion
                // since this will fail and we'll end up with no content being returned.
                client.DefaultContentType = ContentType.RAW;
                try
                {
                    var page = client.GetPage(localization.Namespace(),
                        localization.PublicationId(), urlPath, null, ContentIncludeMode.IncludeDataAndRender, null);
                    return JsonConvert.SerializeObject(page.RawContent.Data);
                }
                catch (Exception)
                {
                    throw new DxaException($"Page URL path '{urlPath}' in Publication '{localization.Id}' returned no data.");
                }
            }
        }

        #region Protected
        protected virtual PageModel LoadPageModel(ref string urlPath, bool addIncludes, Localization localization)
        {
            using (new Tracer(urlPath, addIncludes, localization))
            {
                PageModelData pageModelData = SiteConfiguration.ModelServiceProvider.GetPageModelData(urlPath, localization, addIncludes);

                if (pageModelData == null)
                {
                    throw new DxaItemNotFoundException(urlPath);
                }

                if (pageModelData.MvcData == null)
                {
                    throw new DxaException($"Data Model for Page '{pageModelData.Title}' ({pageModelData.Id}) contains no MVC data. Ensure that the Page is published using the DXA R2 TBBs.");
                }

                return ModelBuilderPipeline.CreatePageModel(pageModelData, addIncludes, localization);
            }
        }

        protected virtual PageModel LoadPageModel(int pageId, bool addIncludes, Localization localization)
        {
            using (new Tracer(pageId, addIncludes, localization))
            {
                PageModelData pageModelData = SiteConfiguration.ModelServiceProvider.GetPageModelData(pageId, localization, addIncludes);

                if (pageModelData == null)
                {
                    throw new DxaItemNotFoundException($"Page not found for publication id {localization.Id} and page id {pageId}");
                }

                if (pageModelData.MvcData == null)
                {
                    throw new DxaException($"Data Model for Page '{pageModelData.Title}' ({pageModelData.Id}) contains no MVC data. Ensure that the Page is published using the DXA R2 TBBs.");
                }

                return ModelBuilderPipeline.CreatePageModel(pageModelData, addIncludes, localization);
            }
        }

        protected virtual EntityModel LoadEntityModel(string id, Localization localization)
        {
            using (new Tracer(id, localization))
            {
                EntityModelData entityModelData = SiteConfiguration.ModelServiceProvider.GetEntityModelData(id, localization);

                if (entityModelData == null)
                {
                    throw new DxaItemNotFoundException(id);
                }

                EntityModel result = ModelBuilderPipeline.CreateEntityModel(entityModelData, null, localization);

                if (result.XpmMetadata != null)
                {
                    // Entity Models requested through this method are per definition "query based" in XPM terminology.
                    result.XpmMetadata["IsQueryBased"] = true; // TODO TSI-24: Do this in Model Service (or CM-side?)
                }

                return result;
            }
        }

        protected virtual EntityModelData CreateEntityModelData(Component component)
        {
            ContentModelData standardMeta = new ContentModelData();
            var groups = component.CustomMetas.Edges.GroupBy(x => x.Node.Key).ToList();
            foreach (var group in groups)
            {
                var values = group.Select(x => x.Node.Value).ToArray();
                if (values.Length == 1)
                    standardMeta.Add(group.Key, values[0]);
                else
                    standardMeta.Add(group.Key, values);
            }

            // The semantic mapping requires that some metadata fields exist. This may not be the case so we map some component meta properties onto them
            // if they don't exist.
            const string dateCreated = "dateCreated";
            if (!standardMeta.ContainsKey(dateCreated))
            {
                standardMeta.Add(dateCreated, component.LastPublishDate);
            }
            else
            {
                if (standardMeta[dateCreated] is string[])
                {
                    standardMeta[dateCreated] = ((string[])standardMeta[dateCreated])[0];
                }
            }
            const string dateTimeFormat = "MM/dd/yyyy HH:mm:ss";
            standardMeta["dateCreated"] = DateTime.ParseExact((string)standardMeta[dateCreated], dateTimeFormat, null);
            if (!standardMeta.ContainsKey("name"))
            {
                standardMeta.Add("name", component.Title);
            }
            return new EntityModelData
            {
                Id = component.ItemId.ToString(),
                SchemaId = component.SchemaId.ToString(),
                Metadata = new ContentModelData { { "standardMeta", standardMeta } }
            };
        }
        #endregion
    }

    #region Cursor Indexing Services
    public interface ICursorIndexerService
    {
        CursorIndexer GetCursorIndexer(string id);
    }

    /// <summary>
    /// CursorIndexer with explicit data synchronization
    /// </summary>
    public class CursorIndexer
    {
        private readonly CursorIndexData _data;
        private readonly Action _saveAction;
        private readonly string _id;
        private readonly Dictionary<string, CursorIndexData> _indexData;

        internal CursorIndexer(
            CursorIndexData data,
            Action saveAction,
            Dictionary<string, CursorIndexData> indexData,
            string id)
        {
            // Always use the dictionary's reference
            var cursorIndexData = indexData[id];
            _data = cursorIndexData;
            _saveAction = saveAction;
            _indexData = indexData;
            _id = id;
        }

        public string this[int index]
        {
            get
            {
                if (_data == null) return null;

                if (index == 0 || _data.Cursors.Count == 0)
                {
                    StartIndex = 0;
                    return null;
                }

                if (_data.Cursors.TryGetValue(index, out var value))
                {
                    StartIndex = index;
                    return value;
                }

                var minKey = _data.Cursors.Keys.Where(k => k < index).DefaultIfEmpty(0).Max();
                StartIndex = minKey;
                return minKey != 0 ? _data.Cursors[minKey] : null;
            }
            set
            {
                if (_data == null) return;

                if (_data.Cursors.ContainsKey(index))
                    _data.Cursors[index] = value;
                else
                    _data.Cursors.Add(index, value);

                _saveAction?.Invoke();
            }
        }

        public int StartIndex
        {
            get => _data.StartIndex;
            set
            {
                _data.StartIndex = value;
                _indexData[_id] = _data; 
                _saveAction?.Invoke();
            }
        }
    }

    /// <summary>
    /// CursorIndexerService
    /// </summary>
    public class CursorIndexerService : ICursorIndexerService
    {
        private const string SessionKey = "dxa_indexer";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CursorIndexerService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CursorIndexer GetCursorIndexer(string id)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return new CursorIndexer(null, null, null, id);

            var session = httpContext.Session;
            session.LoadAsync().GetAwaiter().GetResult();

            var indexData = session.Get<Dictionary<string, CursorIndexData>>(SessionKey)
                          ?? new Dictionary<string, CursorIndexData>();

            if (!indexData.TryGetValue(id, out var cursorData))
            {
                cursorData = new CursorIndexData();
                indexData[id] = cursorData;
                session.Set(SessionKey, indexData);
                session.CommitAsync().GetAwaiter().GetResult();
            }

            return new CursorIndexer(
                cursorData,
                () =>
                {
                    session.Set(SessionKey, indexData);
                    session.CommitAsync().GetAwaiter().GetResult();
                },
                indexData, // Pass the dictionary
                id
            );
        }
    }

    /// <summary>
    /// CursorIndexData
    /// </summary>
    public class CursorIndexData
    {
        public Dictionary<int, string> Cursors { get; set; } = new();
        public int StartIndex { get; set; }
    }

    /// <summary>
    /// Session serialization extensions
    /// </summary>
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            var json = JsonConvert.SerializeObject(value);
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value);
        }
    }
    #endregion
}
