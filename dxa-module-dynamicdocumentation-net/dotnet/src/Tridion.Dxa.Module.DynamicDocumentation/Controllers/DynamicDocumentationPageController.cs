using System;
using Microsoft.AspNetCore.Mvc;
using Sdl.Web.Common;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Modules.DynamicDocumentation.Providers;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Controllers;
using Sdl.Web.Tridion.ApiClient;

namespace Sdl.Web.Modules.DynamicDocumentation.Controllers
{
    /// <summary>
    /// Page Controller
    /// </summary>
    [Area("DynamicDocumentation")]
    public class DynamicDocumentationPageController : BaseController
    {
        private readonly IApiClientFactory _apiClientFactory;
        private readonly IContentProviderExt _contentProviderExt;

        public DynamicDocumentationPageController(IApiClientFactory apiClientFactory, IContentProviderExt contentProviderExt)
        {
            _apiClientFactory = apiClientFactory;
            _contentProviderExt = contentProviderExt;
        }

        [Route("~/")]
        [Route("~/home")]
        [Route("~/publications/{*content}")]
        [HttpGet]
        public ActionResult Home()
        {
            return View("GeneralPage");
        }

        [Route("~/{publicationId:int}")]
        [HttpGet]
        public virtual ActionResult Page(int publicationId)
        {
            return GetPage(publicationId);
        }

        [Route("~/{publicationId:int}/{pageId:int}")]
        [Route("~/{publicationId:int}/{pageId:int}/{*path}")]
        [HttpGet]
        public virtual ActionResult Page(int publicationId, int pageId, string path = "")
        {
            return GetPage(publicationId, pageId);
        }

        protected ActionResult GetPage(int publicationId)
        {
            SetupLocalization(publicationId);
            return View("Areas/DynamicDocumentation/Views/DynamicDocumentationPage/GeneralPage");
        }

        protected ActionResult GetPage(int publicationId, int pageId)
        {
            using (new Tracer(publicationId, pageId))
            {
                try
                {
                    Common.Configuration.Localization localization = SetupLocalization(publicationId);

                    PageModel pageModel;
                    try
                    {
                        pageModel = _contentProviderExt.GetPageModel(pageId, localization);
                    }
                    catch (DxaItemNotFoundException ex)
                    {
                        Log.Info(ex.Message);
                        return NotFound();
                    }

                    PageModelWithHttpResponseData pageModelWithHttpResponseData =
                        pageModel as PageModelWithHttpResponseData;
                    SetupViewData(pageModel);
                    PageModel model = (EnrichModel(pageModel) as PageModel) ?? pageModel;
                    WebRequestContext.Current.PageModel = model;
                    return View(model.MvcData.ViewName, model);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    return ServerError();
                }
            }
        }

        public ActionResult ServerError()
        {
            using (new Tracer())
            {
                Response.StatusCode = 404;             
                ViewResult r = View("ErrorPage");
                r.ViewData.Add("statusCode", Response.StatusCode);
                return r;
            }
        }

        protected Common.Configuration.Localization SetupLocalization(int publicationId)
        {
            PublicationProvider provider = new PublicationProvider(_apiClientFactory);
            provider.CheckPublicationOnline(publicationId);
            Common.Configuration.Localization localization = WebRequestContext.Current.Localization;
            localization.Id = publicationId.ToString();
            return localization;
        }        
    }
}
