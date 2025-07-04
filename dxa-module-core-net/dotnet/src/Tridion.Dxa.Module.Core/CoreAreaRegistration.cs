﻿using Microsoft.AspNetCore.Routing;
using Sdl.Web.Common.Models;
using Sdl.Web.Modules.Core.Models;
using Tridion.Dxa.Framework.Mvc.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Sdl.Web.Modules.Core
{
    public class CoreAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Core";

        public override void Register(IEndpointRouteBuilder endpointRouteBuilder)
        {
            // Map the area route using IEndpointRouteBuilder
            endpointRouteBuilder.MapAreaControllerRoute(
                name: $"{AreaName}_Default",
                areaName: $"{AreaName}",
                pattern: "{controller}/{action}/{id?}",
                defaults: new { controller = "Entity", action = "Entity" }
            );

            RegisterViewModels();
        }

        protected override void RegisterViewModels()
        {
            // Entity Views
            RegisterViewModel("Accordion", typeof(ItemList));
            RegisterViewModel("Article", typeof(Article));
            RegisterViewModel("Carousel", typeof(ItemList));
            RegisterViewModel("CookieNotificationBar", typeof(Notification));
            RegisterViewModel("Download", typeof(Download));
            RegisterViewModel("FooterLinkGroup", typeof(LinkList<Link>));
            RegisterViewModel("FooterLinks", typeof(LinkList<Link>));
            RegisterViewModel("HeaderLinks", typeof(LinkList<Link>));
            RegisterViewModel("HeaderLogo", typeof(Teaser));
            RegisterViewModel("Image", typeof(Image));
            RegisterViewModel("LanguageSelector", typeof(Configuration));
            RegisterViewModel("OldBrowserNotificationBar", typeof(Notification));
            RegisterViewModel("Place", typeof(Place));
            RegisterViewModel("SocialLinks", typeof(LinkList<Sdl.Web.Modules.Core.Models.TagLink>));
            RegisterViewModel("SocialSharing", typeof(LinkList<Sdl.Web.Modules.Core.Models.TagLink>));
            RegisterViewModel("Tab", typeof(ItemList));
            RegisterViewModel("Teaser-ImageOverlay", typeof(Teaser));
            RegisterViewModel("Teaser", typeof(Teaser));
            RegisterViewModel("TeaserColored", typeof(Teaser));
            RegisterViewModel("TeaserHero-ImageOverlay", typeof(Teaser));
            RegisterViewModel("TeaserMap", typeof(Teaser));
            RegisterViewModel("YouTubeVideo", typeof(YouTubeVideo));

            RegisterViewModel("List", typeof(ContentList<Teaser>), "List");
            RegisterViewModel("ArticleList", typeof(ContentList<Article>), "List");
            RegisterViewModel("PagedList", typeof(ContentList<Teaser>), "List");
            RegisterViewModel("ThumbnailList", typeof(ContentList<Teaser>), "List");

            RegisterViewModel("Breadcrumb", typeof(NavigationLinks), "Navigation");
            RegisterViewModel("LeftNavigation", typeof(NavigationLinks), "Navigation");
            RegisterViewModel("SiteMap", typeof(SitemapItem), "Navigation");
            RegisterViewModel("SiteMapXml", typeof(SitemapItem), "Navigation");
            RegisterViewModel("TopNavigation", typeof(NavigationLinks), "Navigation");

            // Page Views
            RegisterViewModel("GeneralPage", typeof(PageModel));
            RegisterViewModel("IncludePage", typeof(PageModel));
            RegisterViewModel("RedirectPage", typeof(PageModel));

            // Region Views
            RegisterViewModel("2-Column", typeof(RegionModel));
            RegisterViewModel("3-Column", typeof(RegionModel));
            RegisterViewModel("4-Column", typeof(RegionModel));
            RegisterViewModel("Multi-Column", typeof(MultiColumnRegion));
            RegisterViewModel("Additional", typeof(RegionModel));
            RegisterViewModel("Article", typeof(RegionModel));
            RegisterViewModel("Content", typeof(RegionModel));
            RegisterViewModel("Hero", typeof(RegionModel));
            RegisterViewModel("Info", typeof(RegionModel));
            RegisterViewModel("Left", typeof(RegionModel));
            RegisterViewModel("Links", typeof(RegionModel));
            RegisterViewModel("Logo", typeof(RegionModel));
            RegisterViewModel("Main Section", typeof(RegionModel));
            RegisterViewModel("Main", typeof(RegionModel));
            RegisterViewModel("Nav", typeof(RegionModel));
            RegisterViewModel("Tools", typeof(RegionModel));

            // Region Views for Include Pages
            RegisterViewModel("Header", typeof(RegionModel));
            RegisterViewModel("Footer", typeof(RegionModel));
            RegisterViewModel("Left Navigation", typeof(RegionModel));
            RegisterViewModel("Content Tools", typeof(RegionModel));
        }
    }
}
