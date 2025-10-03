using Microsoft.AspNetCore.Mvc;
using Sdl.Web.Mvc.Configuration;
using System;
using System.Collections.Generic;

namespace Sdl.Web.Mvc.Formats
{
    public static class DataFormatters
    {
        public static Dictionary<string, IDataFormatter> Formatters { get; set; }

        static DataFormatters()
        {
            Formatters = new Dictionary<string, IDataFormatter>();
        }

        public static IDataFormatter GetFormatter(ControllerContext controllerContext)
        {
            string format = GetFormat(controllerContext);
            if (Formatters.ContainsKey(format) &&
                WebRequestContext.Current.Localization.DataFormats.Contains(format))
            {
                return Formatters[format];
            }
            return null;
        }

        public static List<string> GetValidTypes(ControllerContext controllerContext, List<string> allowedTypes)
        {
            var result = new List<string>();

            var acceptHeader = controllerContext.HttpContext.Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(acceptHeader))
            {
                var acceptTypes = acceptHeader.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var type in acceptTypes)
                {
                    foreach (var mediaType in allowedTypes)
                    {
                        if (type.Contains(mediaType, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(type);
                        }
                    }
                }
            }

            return result;
        }

        public static double GetScoreFromAcceptString(string type)
        {
            double res = 1.0;
            int pos = type.IndexOf("q=", StringComparison.OrdinalIgnoreCase);
            if (pos > 0)
            {
                double.TryParse(type.Substring(pos + 2), out res);
            }
            return res;
        }

        private static string GetFormat(ControllerContext controllerContext)
        {
            var query = controllerContext.HttpContext.Request.Query;
            string format = query.ContainsKey("format") ? query["format"].ToString() : null;

            if (!string.IsNullOrEmpty(format))
            {
                return format.ToLowerInvariant();
            }

            format = "html";
            double topScore = GetHtmlAcceptScore(controllerContext);

            if (topScore < 1.0)
            {
                foreach (var key in Formatters.Keys)
                {
                    double score = Formatters[key].Score(controllerContext);
                    if (score > topScore)
                    {
                        topScore = score;
                        format = key;
                    }

                    if (topScore == 1.0)
                    {
                        break;
                    }
                }
            }

            return format;
        }

        private static double GetHtmlAcceptScore(ControllerContext controllerContext)
        {
            double score = 0.0;

            var acceptHeader = controllerContext.HttpContext.Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(acceptHeader))
            {
                var acceptTypes = acceptHeader.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var type in acceptTypes)
                {
                    if (type.Contains("html", StringComparison.OrdinalIgnoreCase))
                    {
                        double thisScore = GetScoreFromAcceptString(type);
                        if (thisScore > score)
                        {
                            score = thisScore;
                        }
                        if (score == 1.0)
                        {
                            break;
                        }
                    }
                }
            }

            return score;
        }
    }
}
