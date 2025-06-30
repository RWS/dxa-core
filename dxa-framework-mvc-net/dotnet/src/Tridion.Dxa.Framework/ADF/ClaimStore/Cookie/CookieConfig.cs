namespace Tridion.Dxa.Framework.ADF.ClaimStore.Cookie
{
    internal enum CookieType
    {
        ADF,
        TRACKING,
        SESSION
    }

    internal class CookieConfig
    {
        public CookieConfig(string name, string path)
        {
            CookieName = name;
            Path = path;
        }

        public string CookieName { get; protected set; }
        public string Path { get; protected set; }
    }
}
