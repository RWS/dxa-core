using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Controllers;
using System;

namespace Sdl.Web.Mvc.Formats
{
    /// <summary>
    /// Action Filter attritbute used to divert rendering from the standard View to a 
    /// data formatter (for example JSON/RSS), and if necessary enriching the model
    /// by processing all entities to add external data
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class FormatDataAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Controller controller = filterContext.Controller as Controller;
            if (controller != null)
            {
                IDataFormatter formatter = DataFormatters.GetFormatter(controller.ControllerContext);
                if (formatter != null)
                {
                    controller.ViewData[DxaViewDataItems.DisableOutputCache] = true;
                    controller.ViewData[DxaViewDataItems.DataFormatter] = formatter;
                    controller.ViewData[DxaViewDataItems.AddIncludes] = formatter.AddIncludes;
                }
                else
                {
                    controller.ViewData[DxaViewDataItems.DisableOutputCache] = false;
                }
            }

            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            Controller controller = filterContext.Controller as Controller;
            IDataFormatter formatter = controller?.ViewData[DxaViewDataItems.DataFormatter] as IDataFormatter;
            // Once we got here, we expect the View Model to be enriched already, but in case of a Page Model,
            // the embedded Region/Entity Models won't be enriched yet.
            if (formatter != null && formatter.ProcessModel && controller is PageController)
            {
                PageModel pageModel = controller.ViewData.Model as PageModel;
                ((PageController)controller).EnrichEmbeddedModels(pageModel);
                ActionResult result = formatter.FormatData(controller.ControllerContext, pageModel);
                if (result != null)
                {
                    filterContext.Result = result;
                }
            }

            base.OnActionExecuted(filterContext);
        }
    }
}
