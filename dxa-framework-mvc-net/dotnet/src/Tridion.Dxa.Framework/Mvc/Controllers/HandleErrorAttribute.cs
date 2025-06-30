using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Tridion.Dxa.Framework.Mvc.Controllers
{
    public class HandleErrorAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            var result = new ViewResult { ViewName = "Error" };
            var modelMetadata = new EmptyModelMetadataProvider();
            result.ViewData = new ViewDataDictionary(
                modelMetadata, context.ModelState);
            result.ViewData.Add("HandleException",
                context.Exception);
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}
