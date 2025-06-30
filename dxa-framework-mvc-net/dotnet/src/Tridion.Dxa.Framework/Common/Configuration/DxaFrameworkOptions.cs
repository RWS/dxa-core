using System;
using System.Collections.Generic;

namespace Tridion.Dxa.Framework
{
    public class Services
    {
        public Uri Discovery { get; set; }
        public Uri Content { get; set; }
        public Uri IQService { get; set; }
        public Uri Token { get; set; }
    }

    public class OAuthOptions
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public class CacheOptions
    {
        public bool ViewModelCaching { get; set; }
    }

    public class ModelBuilderPipelineConfig
    {
        public string Type { get; set; }
    }

    public class OutputCacheSettings
    {
        public int Duration { get; set; }
        public bool NoStore { get; set; }
        public bool IgnorePreview { get; set; }
        
        // Add other properties as needed
    }

    public class DxaFrameworkOptions
    {
        public bool ClaimForwarding { get; set; }

        public Services Services { get; set; }

        public OAuthOptions OAuth { get; set; }

        public CacheOptions Caching { get; set; }
        public OutputCacheSettings OutputCacheSettings { get; set; }
        public string DefaultModule { get; set; }
        public bool AdminRefreshEnabled { get; set; }
        public string IQSearchIndex { get; set; }
        public bool OutputCachingEnabled { get; set; }
        public string SitemapDefaultDescendantDepth { get; set; }
        public List<ModelBuilderPipelineConfig> ModelBuilderPipelineConfig { get; set; }
        public List<string> IgnoredPaths { get; set; } = new List<string>();

    }
}
