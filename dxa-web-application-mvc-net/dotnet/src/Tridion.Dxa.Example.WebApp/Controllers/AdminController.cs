using Microsoft.AspNetCore.Mvc;
using Sdl.Web.Mvc.Configuration;

namespace Tridion.Dxa.Example.WebApp.Controllers
{
    public class AdminController : Controller
    {
        [ResponseCache(NoStore = true, Duration = 0)]
        public ActionResult Refresh()
        {
            //trigger a reload of config/resources/mappings
            WebRequestContext.Current.Localization.Refresh(allSiteLocalizations: true);
            return Redirect("~" + WebRequestContext.Current.Localization.Path + "/");
        }
    }
}