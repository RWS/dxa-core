using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;


namespace Sdl.Web.Tridion.Statics
{
    /// <summary>
    /// Ensures a Binary file is cached on the file-system from the Tridion Broker DB
    /// </summary>
    public class BinaryFileManager
    {
        #region Inner classes
        internal class Dimensions
        {
            internal int Width;
            internal int Height;
            internal bool NoStretch;

            /// <summary>
            /// Returns a string that represents the current object.
            /// </summary>
            /// <returns>
            /// A string that represents the current object.
            /// </returns>
            public override string ToString()
            {
                return $"(W={Width}, H={Height}, NoStretch={NoStretch})";
            }
        }
        #endregion

        /// <summary>
        /// Gets the singleton BinaryFileManager instance.
        /// </summary>
        internal static BinaryFileManager Instance { get; } = new BinaryFileManager();

        internal static IBinaryProvider Provider => SiteConfiguration.BinaryProvider;

        private static bool IsCached(Func<DateTime> getLastPublishedDate, string localFilePath,
            Localization localization)
        {
            DateTime lastPublishedDate = SiteConfiguration.CacheProvider.GetOrAdd(
                localFilePath,
                CacheRegions.BinaryPublishDate,
                getLastPublishedDate
                );

            if (localization.LastRefresh != DateTime.MinValue && localization.LastRefresh.CompareTo(lastPublishedDate) < 0)
            {
                //File has been modified since last application start
                Log.Debug(
                    "Binary at path '{0}' is modified",
                    localFilePath);
                return false;
            }

            FileInfo fi = new FileInfo(localFilePath);
            if (fi.Length > 0)
            {
                DateTime fileModifiedDate = File.GetLastWriteTime(localFilePath);
                if (fileModifiedDate.CompareTo(lastPublishedDate) >= 0)
                {
                    Log.Debug("Binary at path '{0}' is still up to date, no action required", localFilePath);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the cached local file for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path.</param>
        /// <param name="localization">The Localization.</param>
        /// <returns>The path to the local file.</returns>
        internal string GetCachedFile(string urlPath, Localization localization, out MemoryStream memoryStream)
        {
            memoryStream = null;
            IBinaryProvider provider = Provider;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFilePath = $"{baseDir}/{urlPath}";
            if (File.Exists(localFilePath))
            {
                // If our resource exists on the filesystem we can assume static content that is
                // manually added to web application.
                return localFilePath;
            }
            // Attempt cache location with fallback to retrieval from content service.
            localFilePath = $"{baseDir}/{localization.BinaryCacheFolder}/{urlPath}";
            using (new Tracer(urlPath, localization, localFilePath))
            {
                Dimensions dimensions;
                urlPath = StripDimensions(urlPath, out dimensions);
                if (File.Exists(localFilePath))
                {
                    if (IsCached(() => provider.GetBinaryLastPublishedDate(localization, urlPath), localFilePath, localization))
                    {
                        return localFilePath;
                    }
                }

                var binary = provider.GetBinary(localization, urlPath);
                WriteBinaryToFile(binary.Item1, localFilePath, dimensions, out memoryStream);
                return localFilePath;
            }
        }

        /// <summary>
        /// Gets the cached local file for a given binary Id.
        /// </summary>
        /// <param name="binaryId">The binary Id.</param>
        /// <param name="localization">The Localization.</param>
        /// <returns>The path to the local file.</returns>
        internal string GetCachedFile(int binaryId, Localization localization, out MemoryStream memoryStream)
        {
            memoryStream = null;
            IBinaryProvider provider = Provider;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFilePath = $"{baseDir}/{localization.BinaryCacheFolder}";
            using (new Tracer(binaryId, localization, localFilePath))
            {
                try
                {
                    if (Directory.Exists(localFilePath))
                    {
                        string[] files = Directory.GetFiles(localFilePath, $"{binaryId}*",
                            SearchOption.TopDirectoryOnly);
                        if (files.Length > 0)
                        {
                            localFilePath = files[0];
                            if (IsCached(() => provider.GetBinaryLastPublishedDate(localization, binaryId),
                                localFilePath,
                                localization))
                            {
                                return localFilePath;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Our binary cache folder probably doesn't exist. 
                    Log.Warn($"Failed to cache binary at {localFilePath}");
                    Log.Warn(ex.Message);
                }

                var data = provider.GetBinary(localization, binaryId);
                if (string.IsNullOrEmpty(Path.GetExtension(localFilePath)))
                {
                    var ext = Path.GetExtension(data.Item2) ?? "";
                    localFilePath = $"{localFilePath}/{binaryId}{ext}";
                }

                WriteBinaryToFile(data.Item1, localFilePath, null, out memoryStream);
                return localFilePath;
            }
        }

        /// <summary>
        /// Perform actual write of binary content to file
        /// </summary>
        /// <param name="binary">The binary to store</param>
        /// <param name="physicalPath">String the file path to write to</param>
        /// <param name="dimensions">Dimensions of file</param>
        /// <returns>True is binary was written to disk, false otherwise</returns>
        private static void WriteBinaryToFile(byte[] binary, string physicalPath, Dimensions dimensions, out MemoryStream memoryStream)
        {
            memoryStream = null;
            if (binary == null) return;
            byte[] buffer = binary;
            using (new Tracer(binary, physicalPath, dimensions))
            {
                try
                {
                    if (!File.Exists(physicalPath))
                    {
                        FileInfo fileInfo = new FileInfo(physicalPath);
                        if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                        {
                            fileInfo.Directory.Create();
                        }
                    }
                   
                    if (dimensions != null && (dimensions.Width > 0 || dimensions.Height > 0))
                    {
                        string imgFormat = GetImageFormat(physicalPath);
                        if (imgFormat != null) buffer = ResizeImage(buffer, dimensions, imgFormat);
                    }

                    lock (NamedLocker.GetLock(physicalPath))
                    {
                        using (FileStream fileStream = new FileStream(physicalPath, FileMode.Create,
                            FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            fileStream.Write(buffer, 0, buffer.Length);
                        }
                    }

                    NamedLocker.RemoveLock(physicalPath);
                }
                catch (IOException)
                {
                    // file possibly accessed by a different thread in a different process, locking failed
                    Log.Warn("Cannot write to {0}. This can happen sporadically, let the next thread handle this.", physicalPath);
                    Thread.Sleep(1000);
                    memoryStream = new MemoryStream(buffer);
                }
            }
        }

        private static void CleanupLocalFile(string physicalPath)
        {
            using (new Tracer(physicalPath))
            {
                try
                {
                    // file got unpublished
                    File.Delete(physicalPath);
                    NamedLocker.RemoveLock(physicalPath);
                }
                catch (IOException)
                {
                    // file probabaly accessed by a different thread in a different process
                    Log.Warn("Cannot delete '{0}'. This can happen sporadically, let the next thread handle this.", physicalPath);
                }
            }
        }

        internal static byte[] ResizeImage(byte[] imageData, Dimensions dimensions, string imageFormat)
        {
            using (new Tracer(imageData.Length, dimensions, imageFormat))
            {
                // Load the image using ImageSharp
                using (Image image = Image.Load(imageData))
                {
                    // Default for crop position, width, and target size
                    int cropX = 0, cropY = 0;
                    int sourceW = image.Width, sourceH = image.Height;
                    int targetW = image.Width, targetH = image.Height;

                    //Most complex case is if a height AND width is specified
                    if (dimensions.Width > 0 && dimensions.Height > 0)
                    {
                        if (dimensions.NoStretch)
                        {
                            // If we don't want to stretch, then we crop
                            float originalAspect = (float)image.Width / (float)image.Height;
                            float targetAspect = (float)dimensions.Width / (float)dimensions.Height;
                            if (targetAspect < originalAspect)
                            {
                                //Crop the width - ensuring that we do not stretch if the requested height is bigger than the original
                                targetH = dimensions.Height > image.Height ? image.Height : dimensions.Height;
                                targetW = (int)Math.Ceiling(targetH * targetAspect);
                                cropX = (int)Math.Ceiling((image.Width - (image.Height * targetAspect)) / 2);
                                sourceW = sourceW - 2 * cropX;
                            }
                            else
                            {
                                //Crop the height - ensuring that we do not stretch if the requested width is bigger than the original
                                targetW = dimensions.Width > image.Width ? image.Width : dimensions.Width;
                                targetH = (int)Math.Ceiling(targetW / targetAspect);
                                cropY = (int)Math.Ceiling((image.Height - (image.Width / targetAspect)) / 2);
                                sourceH = sourceH - 2 * cropY;
                            }
                        }
                        else
                        {
                            // Stretch to fit the dimensions
                            targetH = dimensions.Height;
                            targetW = dimensions.Width;
                        }
                    }
                //If we simply have a certain width or height, its simple: We just use that and derive the other
                //dimension from the original image aspect ratio. We also check if the target size is bigger than
                //the original, and if we allow stretching.
                    else if (dimensions.Width > 0)
                    {
                        targetW = (dimensions.NoStretch && dimensions.Width > image.Width) ? image.Width : dimensions.Width;
                        targetH = (int)(image.Height * ((float)targetW / (float)image.Width));
                    }
                    else
                    {
                        targetH = (dimensions.NoStretch && dimensions.Height > image.Height) ? image.Height : dimensions.Height;
                        targetW = (int)(image.Width * ((float)targetH / (float)image.Height));
                    }

                    if (targetW == image.Width && targetH == image.Height)
                    {
                        // No resize required
                        return imageData;
                    }

                    // Resize the image
                    image.Mutate(x => x.Resize(targetW, targetH));

                    // Save the image to a memory stream in the specified format
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        // Determine the format to use and save the image accordingly
                        switch (imageFormat.ToLower())
                        {
                            case "jpeg":
                            case "jpg":
                                image.SaveAsJpeg(memoryStream);
                                break;
                            case "png":
                                image.SaveAsPng(memoryStream);
                                break;
                            case "bmp":
                                image.SaveAsBmp(memoryStream);
                                break;
                            case "gif":
                                image.SaveAsGif(memoryStream);
                                break;
                            default:
                                throw new InvalidOperationException("Unsupported image format");
                        }

                        return memoryStream.ToArray();
                    }
                }
            }
        }

        public static string GetImageFormat(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            switch (Path.GetExtension(path).ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    return "jpeg";
                case ".gif":
                    return "gif";
                case ".png":
                    return "png";
                case ".bmp":
                    return "bmp";
                default:
                    return null;
            }
        }

        internal static string StripDimensions(string path, out Dimensions dimensions)
        {
            dimensions = new Dimensions();
            Regex re = new Regex(@"_(w(\d+))?(_h(\d+))?(_n)?\.");
            if (re.IsMatch(path))
            {
                Match match = re.Match(path);
                string dim = match.Groups[2].ToString();
                if (!string.IsNullOrEmpty(dim))
                {
                    dimensions.Width = Convert.ToInt32(dim);
                }
                dim = match.Groups[4].ToString();
                if (!string.IsNullOrEmpty(dim))
                {
                    dimensions.Height = Convert.ToInt32(dim);
                }
                if (!string.IsNullOrEmpty(match.Groups[5].ToString()))
                {
                    dimensions.NoStretch = true;
                }
                return re.Replace(path, ".");
            }

            // TSI-417: unescape and only escape spaces
            path = WebUtility.UrlDecode(path);
            path = path.Replace(" ", "%20");
            return path;
        }
    }
}
