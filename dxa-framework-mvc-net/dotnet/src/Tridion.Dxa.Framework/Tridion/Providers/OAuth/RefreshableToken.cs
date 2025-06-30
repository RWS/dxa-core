namespace Tridion.Dxa.Framework.Tridion.Providers.OAuth
{
    public class RefreshableToken : Token
    {
        /// <summary>
        /// The Refresh Token retrieved from the token provider
        /// </summary>        
        public string RefreshToken { get; set; }
    }
}
