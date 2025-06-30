using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core.JSON.NET
{
    /// <summary>
    /// Time Utils
    /// </summary>
    public class TimeUtils
    {
        public static string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";

        public static DateTime ParseDate(string stringDate) => DateTime.ParseExact(stringDate, DATE_FORMAT, CultureInfo.InvariantCulture);

        public static DateTime ParseDate(string stringDate, string dateFormat) => DateTime.ParseExact(stringDate, dateFormat, CultureInfo.InvariantCulture);
    }
}
