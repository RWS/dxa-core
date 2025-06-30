using Microsoft.AspNetCore.Routing;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Html;
using System;

namespace Tridion.Dxa.Framework.Mvc.Configuration
{
    public abstract class AreaRegistration
    {
        public abstract string AreaName { get; }
        public abstract void Register(IEndpointRouteBuilder endpointRouteBuilder);

        /// <summary>
        /// Registers a View Model without associated View.
        /// </summary>
        /// <param name="modelType">The View Model type.</param>
        /// <remarks>
        /// </remarks>
        protected void RegisterViewModel(Type modelType)
        {
            ModelTypeRegistry.RegisterViewModel(null, modelType);
        }

        /// <summary>
        /// Registers a View Model and associated View.
        /// </summary>
        /// <param name="viewName">The name of the View to register.</param>
        /// <param name="modelType">The View Model Type to associate with the View. Must be a subclass of Type <see cref="ViewModel"/>.</param>
        /// <param name="controllerName">The Controller name. If not specified (or <c>null</c>), the Controller name is inferred from the <see cref="modelType"/>: either "Entity", "Region" or "Page".</param>
        protected void RegisterViewModel(string viewName, Type modelType, string controllerName = null)
        {
            if (string.IsNullOrEmpty(controllerName))
            {
                controllerName = DetermineControllerName(modelType);
            }

            var mvcData = new MvcData { AreaName = AreaName, ControllerName = controllerName, ViewName = viewName };
            ModelTypeRegistry.RegisterViewModel(mvcData, modelType);
        }

        private string DetermineControllerName(Type modelType)
            => modelType switch
            {
                _ when typeof(EntityModel).IsAssignableFrom(modelType) => "Entity",
                _ when typeof(RegionModel).IsAssignableFrom(modelType) => "Region",
                _ => "Page"
            };

        //
        // Summary:
        //     Registers a Sdl.Web.Mvc.Html.IMarkupDecorator implementation.
        //
        // Parameters:
        //   markupDecoratorType:
        //     The type of the markup decorator. The type must have a parameterless constructor
        //     and implement Sdl.Web.Mvc.Html.IMarkupDecorator.
        protected void RegisterMarkupDecorator(Type markupDecoratorType)
        {
            Markup.RegisterMarkupDecorator(markupDecoratorType);
        }

        protected virtual void RegisterViewModels()
        {
            // folder scan?
        }
    }
}
