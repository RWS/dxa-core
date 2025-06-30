using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using System;

namespace Sdl.Web.Mvc.Controllers
{
    /// <summary>
    /// Abstract base class for DXA Controllers 
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Gets or sets the Content Provider.
        /// </summary>
        /// <remarks>
        /// Setting this property is no longer needed, but setter is kept for backwards compatibility.
        /// </remarks>
        protected IContentProvider ContentProvider { get; set; }

        /// <summary>
        /// Enriches the View Model as obtained from the Content Provider.
        /// </summary>
        /// <param name="model">The View Model to enrich.</param>
        /// <returns>The enriched View Model.</returns>
        /// <remarks>
        /// This is the method to override if you need to add custom model population logic. 
        /// For example retrieving additional information from another system.
        /// </remarks>
        protected virtual ViewModel EnrichModel(ViewModel model)
            => model;

        protected virtual ActionResult GetRawActionResult(string type, string rawContent)
        {
            string contentType;
            switch (type)
            {
                case "json":
                    contentType = "application/json";
                    break;
                case "xml":
                case "rss":
                case "atom":
                    contentType = type.Equals("xml") ? "text/xml" : String.Format("application/{0}+xml", type);
                    break;
                default:
                    contentType = "text/" + type;
                    break;
            }
            return Content(rawContent, contentType);
        }

        protected virtual void SetupViewData(int containerSize = 0, MvcData viewData = null)
        {
            ViewData[DxaViewDataItems.ContainerSize] = containerSize;
            if (viewData != null)
            {
                ViewData[DxaViewDataItems.RegionName] = viewData.RegionName;
                //This enables us to jump areas when rendering sub-views - for example from rendering a region in Core to an entity in ModuleX
                ControllerContext.RouteData.DataTokens["area"] = viewData.AreaName;
            }
        }

        protected virtual void SetupViewData(ViewModel viewModel, int containerSize = 0)
        {
            if (viewModel == null) 
            {
                Log.Warn("Attempted to setup view data with null view model.");
                return;
            }

            SetupViewData(containerSize, viewModel.MvcData);
        }

        /// <summary>
        /// Gets the typed value of a request parameter (from the URL query string) with a given name.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="name">The name of the parameter.</param>
        /// <returns>The typed value of the request parameter or the default value for the given type if the parameter is not specified or cannot be converted to the type.</returns>
        protected virtual T GetRequestParameter<T>(string name)
        {
            T value;
            TryGetRequestParameter(name, out value);
            return value;
        }

        /// <summary>
        /// Tries to get the typed value of a request parameter (from the URL query string) with a given name.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The typed value of the parameter (output).</param>
        /// <returns><c>true</c> if the parameter is specified and its value can be converted to the given type; <c>false</c> otherwise.</returns>
        protected bool TryGetRequestParameter<T>(string name, out T value)
        {
            string paramValue = HttpContext.Request.Query[name];
            if (string.IsNullOrEmpty(paramValue))
            {
                Log.Debug("Request parameter '{0}' is not specified.", name);
                value = default(T);
                return false;
            }

            try
            {
                value = (T)Convert.ChangeType(paramValue, typeof(T));
                return true;
            }
            catch (Exception)
            {
                Log.Warn("Could not convert value for request parameter '{0}' into type {1}. Value: '{2}'.", name, typeof(T).Name, paramValue);
                value = default(T);
                return false;
            }
        }

        /// <summary>
        /// Enriches a given Entity Model using an appropriate (custom) Controller.
        /// </summary>
        /// <param name="entity">The Entity Model to enrich.</param>
        /// <returns>The enriched Entity Model.</returns>
        /// <remarks>
        /// This method is different from <see cref="EnrichModel"/> in that it doesn't expect the current Controller to be able to enrich the Entity Model;
        /// it creates a Controller associated with the Entity Model for that purpose.
        /// It is used by <see cref="PageController.EnrichEmbeddedModels"/>.
        /// </remarks>
        protected EntityModel EnrichEntityModel(EntityModel entity)
        {
            if (entity == null || entity.MvcData == null || !IsCustomAction(entity.MvcData))
            {
                return entity;
            }

            MvcData mvcData = entity.MvcData;
            using (new Tracer(entity, mvcData))
            {
                string controllerName = mvcData.ControllerName ?? SiteConfiguration.GetEntityController();
                string controllerAreaName = mvcData.ControllerAreaName ?? SiteConfiguration.GetDefaultModuleName();

                var tempRouteData = new RouteData();
                tempRouteData.DataTokens["Area"] = controllerAreaName;
                tempRouteData.Values["controller"] = controllerName;
                tempRouteData.Values["area"] = controllerAreaName;

                var tempHttpContext = new DefaultHttpContext();
                var tempActionContext = new ActionContext(tempHttpContext, tempRouteData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

                var controllerFactory = HttpContext.RequestServices.GetRequiredService<IControllerFactory>();
                var entityController = (BaseController)controllerFactory.CreateController(new ControllerContext(tempActionContext));

                entityController.ControllerContext = new ControllerContext(tempActionContext);
                return (EntityModel)entityController.EnrichModel(entity);
            }
        }

        private static bool IsCustomAction(MvcData mvcData)
        {
            return mvcData.ActionName != SiteConfiguration.GetEntityAction()
                || mvcData.ControllerName != SiteConfiguration.GetEntityController()
                || mvcData.ControllerAreaName != SiteConfiguration.GetDefaultModuleName();
        }

        public override JsonResult Json(object data, object serializerSettings)
        {
            return new JsonResult(data, serializerSettings);
        }
    }
}
