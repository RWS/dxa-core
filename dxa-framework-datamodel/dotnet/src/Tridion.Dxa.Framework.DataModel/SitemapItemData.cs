using System;
using System.Collections.Generic;

namespace Sdl.Web.DataModel
{
    public class SitemapItemData
    {
        public SitemapItemData()
        {
            Items = new List<SitemapItemData>();
        }

        public string Title { get; set; }

        public string Url { get; set; }

        public string Id { get; set; }
        public string Type { get; set; }
        public List<SitemapItemData> Items { get; set; }
        public DateTime? PublishedDate { get; set; }
        public bool Visible { get; set; }
    }
}
