using Microsoft.AspNetCore.Mvc;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Models;
using Tridion.Dxa.Framework.Mvc.Controllers;

namespace Sdl.Web.Mvc.Controllers
{
    public class RegionController : BaseController
    {
        public RegionController(IContentProvider contentProvider)
        {
            ContentProvider = contentProvider;
        }

        /// <summary>
        /// Map and render a region model
        /// </summary>
        /// <param name="region">The region model</param>
        /// <param name="containerSize">The size (in grid units) of the container the region is in</param>
        /// <returns>Rendered region model</returns>
        public virtual ActionResult Region([ModelBinder(typeof(DxaModelBinder))] RegionModel region, int containerSize = 0)
        {
            SetupViewData(region, containerSize);
            RegionModel model = (EnrichModel(region) as RegionModel) ?? region;
            return View(model.MvcData.ViewName, model);
        }
    }
}
