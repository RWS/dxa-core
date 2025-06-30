using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

namespace Sdl.Web.Mvc.Formats
{
    /// <summary>
    /// JSON ActionResult using JSON.NET serializer instead of the JavaScriptSerializer which is used by default in ASP.NET MVC. 
    /// </summary>
    /// <remarks>
    /// Based on code in this post: http://james.newtonking.com/archive/2008/10/16/asp-net-mvc-and-json-net
    /// </remarks>
    public class JsonNetResult : JsonResult
    {
        public Formatting Formatting { get; set; }

        public JsonNetResult(object value) : base(value)
        {
            SerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
        }

        public override void ExecuteResult(ActionContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var response = context.HttpContext.Response;
            response.ContentType = ContentType ?? "application/json";

            if (Value != null)
            {
                JsonTextWriter writer = new JsonTextWriter(new HttpResponseStreamWriter(response.Body, Encoding.Default)) { Formatting = Formatting };
                JsonSerializer serializer = JsonSerializer.Create((JsonSerializerSettings)SerializerSettings);
                serializer.Serialize(writer, Value);
                writer.Flush();
            }
        }
    }
}
