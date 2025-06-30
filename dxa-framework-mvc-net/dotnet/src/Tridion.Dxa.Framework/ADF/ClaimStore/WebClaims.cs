namespace Tridion.Dxa.Framework.ADF.ClaimStore
{
    /// <summary>
    /// WebClaims
    /// </summary>
    public static class WebClaims
    {
        public static readonly string REQUEST_URI = "taf:request:uri";
        public static readonly string REQUEST_FULL_URL = "taf:request:full_url";
        public static readonly string REQUEST_HEADERS = "taf:request:headers";
        public static readonly string RESPONSE_HEADERS = "taf:response:headers";
        public static readonly string REQUEST_PARAMETERS = "taf:request:parameters";
        public static readonly string REQUEST_COOKIES = "taf:request:cookies";
        public static readonly string SERVER_VARIABLES = "taf:server:variables";
        public static readonly string SESSION_ID = "taf:session:id";
        public static readonly string TRACKING_ID = "taf:tracking:id";
        public static readonly string SESSION_ATTRIBUTES = "taf:session:attributes";
        public static readonly string DEFAULT_COOKIE_CLAIM = "taf:response:cookie:generation";
        public static readonly string USER_ID_PROPERTY = "taf:claim:contentdelivery:webservice:user";
        public static readonly string COOKIE_FORWARDING_CLAIM = "taf:claim:contentdelivery:webservice:client:AllowedCookieForwarding";
    }
}
