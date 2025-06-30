using Microsoft.AspNetCore.Mvc.Filters;
using System;
using Tridion.Dxa.Framework.Mvc.Controllers;

namespace Sdl.Web.Mvc.Controllers
{
    /// <summary>
    /// Handle error attribute for sub-sections of pages (entities/regions) which renders view for the error, but does not prevent the rest of the page being rendered
    /// </summary>
    public class HandleSectionErrorAttribute : HandleErrorAttribute
    {
        public string View { get; set; }
        public override void OnException(ExceptionContext filterContext)
        {
            base.OnException(filterContext);
        }
    }


    public class HandleErrorInfo
    {
        /// <summary>Initializes a new instance of the <see cref="T:System.Web.Mvc.HandleErrorInfo" /> class.</summary>
        /// <param name="exception">The exception.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="exception" /> parameter is null.</exception>
        /// <exception cref="T:System.ArgumentException">The <paramref name="controllerName" /> or <paramref name="actionName" /> parameter is null or empty.</exception>
        public HandleErrorInfo(Exception exception, string controllerName, string actionName)
        {
            this.Exception = exception;
            this.ControllerName = controllerName;
            this.ActionName = actionName;
        }

        /// <summary>Gets or sets the name of the action that was executing when the exception was thrown.</summary>
        /// <returns>The name of the action.</returns>
        public string ActionName { get; private set; }

        /// <summary>Gets or sets the name of the controller that contains the action method that threw the exception.</summary>
        /// <returns>The name of the controller.</returns>
        public string ControllerName { get; private set; }

        /// <summary>Gets or sets the exception object.</summary>
        /// <returns>The exception object.</returns>
        public Exception Exception { get; private set; }
    }
}
