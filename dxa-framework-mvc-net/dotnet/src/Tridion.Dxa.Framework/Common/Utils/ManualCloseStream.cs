using System.IO;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Common.Utils
{
    public class ManualCloseStream : MemoryStream
    {
        public bool StayOpen { get; set; }

        public ManualCloseStream(bool stayOpen) : base()
        {
            StayOpen = stayOpen;
        }

        public override void Close()
        {
            if (StayOpen) return;
            base.Close();
        }

        public void ForceClose()
        {
            base.Close();
            StayOpen = false;
        }

        public string ReadToEndAndClose()
        {
            Position = 0;
            string result;
            using (var sr = new StreamReader(this))
            {
                result = sr.ReadToEnd();
            }
            ForceClose();
            return result;
        }

        public async Task<string> ReadToEndAndCloseAsync()
        {
            Position = 0;
            string result;
            using (var sr = new StreamReader(this))
            {
                result = await sr.ReadToEndAsync();
            }
            ForceClose();
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
