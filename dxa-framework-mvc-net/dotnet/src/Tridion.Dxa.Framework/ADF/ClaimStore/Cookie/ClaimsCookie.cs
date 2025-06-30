namespace Tridion.Dxa.Framework.ADF.ClaimStore.Cookie
{
    public class ClaimsCookie
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The name of the cookie</param>
        /// <param name="value">The value of the cookie</param>
        public ClaimsCookie(string name, byte[] value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// The name of the cookie.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// The value of the cookie.
        /// </summary>
        public byte[] Value
        {
            get;
            set;
        }
    }
}
