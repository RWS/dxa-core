namespace Tridion.Dxa.Framework.ADF.ClaimStore.Cookie
{
    /// <summary>
    /// CookieHelpers
    /// </summary>
    //internal static class CookieHelpers
    //{
    //    public static byte[] GetCookieValue(HttpCookie cookie)
    //    {
    //        string value = cookie.Value;
    //        value = value.Replace(",$Version=1", "");
    //        return Encoding.UTF8.GetBytes(value);
    //    }

    //    public static bool IsCookieValid(HttpCookie cookie)
    //    {            
    //        if (AmbientRuntime.Instance.HttpHeaderProcessor.IsProcessorEnabled)
    //        {
    //            if (Logger.AllowTrace) Logger.Trace("http header processor enabled.");
    //            string cookieValue = cookie.Value;
    //            if (cookieValue != null && AmbientRuntime.Instance.HttpHeaderProcessor.ValidateHttpHeader(cookieValue))
    //            {
    //                return true;
    //            }
    //            if (!AmbientRuntime.Instance.HttpHeaderProcessor.IsValidationActive)
    //            {
    //                return true;
    //            }
    //        }
    //        else
    //        {
    //            return true;
    //        }
    //        return true;
    //    }

    //    public static bool IsValidCookieValue(string trackingIdCookieValue) => true;

    //    private static string StripPossibleCookieValueDigest(string cookieValue) => AmbientRuntime.Instance.HttpHeaderProcessor.CleanContent(cookieValue);

    //    public static bool IsCookieForwardingAllowed(IClaimStore claimStore)
    //    {
    //        bool result = false;
    //        try
    //        {               
    //            bool accountAllowsCookieForwarding = false;
    //            bool whiteListCondition = false;

    //            // Check if cookie forwarding is enabled per account.             
    //            string value = claimStore.Get<string>(new Uri(WebClaims.COOKIE_FORWARDING_CLAIM));
    //            accountAllowsCookieForwarding = ("true".Equals(value, StringComparison.OrdinalIgnoreCase)) ? true : false;

    //            if (Logger.AllowDebug) Logger.Debug("Cookie forwarding for account is set to: " + accountAllowsCookieForwarding);

    //            // not supported right now as this should be done in Odata service
    //            // if cookie forwarding not enabled per account makes sense to consult white list.
    //            //if (!accountAllowsCookieForwarding)
    //            //{
    //            //    whiteListCondition = (whiteListFilter != null && whiteListFilter.IsValid(claimStore));
    //            //    Logger.Debug("IP address is in the white list: " + whiteListCondition);
    //            // }

    //            result = accountAllowsCookieForwarding || whiteListCondition;
    //            if (Logger.AllowDebug) Logger.Debug("Cookie forwarding for current request is allowed: " + result);
    //        }
    //        catch (Exception e)
    //        {
    //            Logger.Error("Wrong cookie forwarding claim name:" + e.Message);
    //        }

    //        return result;
    //    }

    //    public static bool GetOrCreateIdAndCookieValue(string name, ref string idCookieValue, out string id)
    //    {
    //        bool newIdGenerated = false;

    //        if (string.IsNullOrEmpty(idCookieValue) || !IsValidCookieValue(idCookieValue))
    //        {
    //            // Create new ID and calculate cookie value
    //            id = AmbientDataConfig.Instance.InstanceId + GuidHelpers.GenerateUniqueId;
    //            if (Logger.AllowDebug) Logger.Debug(String.Format("Generated new {0}: {1}", name, id));
    //            idCookieValue = AmbientRuntime.Instance.HttpHeaderProcessor.CreateValidHttpHeader(id);
    //            newIdGenerated = true;
    //        }
    //        else
    //        {
    //            id = StripPossibleCookieValueDigest(idCookieValue);
    //            if (id == idCookieValue)
    //            {
    //                // There was no digest, add it if configured
    //                idCookieValue = AmbientRuntime.Instance.HttpHeaderProcessor.CreateValidHttpHeader(id);
    //                newIdGenerated = (id != idCookieValue);
    //            }
    //        }

    //        return newIdGenerated;
    //    }

    //    public static HttpCookie CreateInstanceBoundUniqueIdCookie(CookieConfig cookieConfig)
    //    {
    //        HttpCookie cookie = new HttpCookie(cookieConfig.CookieName);
    //        string cookiePath = cookieConfig.Path;

    //        if (!string.IsNullOrEmpty(cookiePath))
    //        {
    //            cookie.Path = cookiePath;
    //        }

    //        cookie.HttpOnly = true;
    //        return cookie;
    //    }
    //}
}
