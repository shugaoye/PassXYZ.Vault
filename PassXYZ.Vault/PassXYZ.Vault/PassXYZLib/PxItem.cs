﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using System.Text;

using HtmlAgilityPack;
using Xamarin.Forms;
using Xamarin.Essentials;
using SkiaSharp;

using FontAwesome.Regular;
using KeePassLib;
using KeePassLib.Utility;

namespace PassXYZLib
{
    /// <summary>
    /// ItemExtensions is a static class which defines a set of extension methods for Item.
    /// </summary>
    public static class PxItem
    {
        private static bool UrlExists(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    // Check URL format
                    Uri uri = new Uri(url);
                    if (uri.Scheme.Contains("http") || uri.Scheme.Contains("https"))
                    {
                        var webRequest = WebRequest.Create(url);
                        webRequest.Method = "HEAD";
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                        return webResponse.StatusCode == HttpStatusCode.OK;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
            return false;
        }

        private static string FormatUrl(string url, string baseUrl)
        {
            if (url.StartsWith("//")) { return ("http:" + url); }
            else if (url.StartsWith("/")) { return (baseUrl + url); }

            return url;
        }

        public static string RetrieveFavicon(string url)
        {
            string returnFavicon = null;

            // declare htmlweb and load html document
            HtmlWeb web = new HtmlWeb();
            var htmlDoc = web.Load(url);

            //1. 处理 apple-touch-icon 的情况
            var elementsAppleTouchIcon = htmlDoc.DocumentNode.SelectNodes("//link[contains(@rel, 'apple-touch-icon')]");
            if (elementsAppleTouchIcon != null && elementsAppleTouchIcon.Any())
            {
                var favicon = elementsAppleTouchIcon.First();
                var faviconUrl = FormatUrl(favicon.GetAttributeValue("href", null), url);
                if (UrlExists(faviconUrl))
                {
                    return faviconUrl;
                }
            }

            // 2. Try to get svg version
            var el = htmlDoc.DocumentNode.SelectSingleNode("/html/head/link[@rel='icon' and @href]");
            if (el != null)
            {
                try
                {
                    var faviconUrl = FormatUrl(el.Attributes["href"].Value, url);

                    if (UrlExists(faviconUrl))
                    {
                        return faviconUrl;
                    }
                }
                catch (WebException ex)
                {
                    Debug.WriteLine($"{ex}");
                }
            }

            // 3. 从页面的 HTML 中抓取
            var elements = htmlDoc.DocumentNode.SelectNodes("//link[contains(@rel, 'icon')]");
            if (elements != null && elements.Any())
            {
                var favicon = elements.First();
                var faviconUrl = FormatUrl(favicon.GetAttributeValue("href", null), url);
                if (UrlExists(faviconUrl))
                {
                    return faviconUrl;
                }
            }

            // 4. 直接获取站点的根目录图标
            try
            {
                var uri = new Uri(url);
                if (uri.HostNameType == UriHostNameType.Dns)
                {
                    var faviconUrl = string.Format("{0}://{1}/favicon.ico", uri.Scheme == "https" ? "https" : "http", uri.Host);
                    if (UrlExists(faviconUrl))
                    {
                        return faviconUrl;
                    }
                }
            }
            catch (UriFormatException ex)
            {
                Debug.WriteLine($"{ex}");
                return returnFavicon;
            }

            return returnFavicon;
        }

        /// <summary>
        /// Create a SKBitmap instance from a byte arrary
        /// </summary>
		/// <param name="pb">byte arraty</param>
		/// <param name="url">This is the url using to retrieve icon.</param>
        public static SKBitmap LoadImage(byte[] pb, string faviconUrl = null)
        {
            int w = 96, h = 96;
            if (DeviceInfo.Platform.Equals(DevicePlatform.Android))
            {
                w = 96; h = 96;
            }
            else if (DeviceInfo.Platform.Equals(DevicePlatform.iOS))
            {
                w = 64; h = 64;
            }
            else if (DeviceInfo.Platform.Equals(DevicePlatform.UWP))
            {
                w = 32; h = 32;
            }

            if (faviconUrl != null) 
            {
                if (faviconUrl.EndsWith(".ico") || faviconUrl.EndsWith(".png"))
                {
                    return GfxUtil.ScaleImage(GfxUtil.LoadImage(pb), w, h);
                }
                else if (faviconUrl.EndsWith(".svg"))
                {
                    return GfxUtil.LoadSvgImage(pb, w, h);
                }
                else { return null; }
            }
            else 
            {
                return GfxUtil.ScaleImage(GfxUtil.LoadImage(pb), w, h);
            }

        }

        public static ImageSource GetImageSource(SKBitmap bitmap)
        {
            if (bitmap != null)
            {
                //SKImage image = SKImage.FromPixels(bitmap.PeekPixels());
                //SKData encoded = image.Encode();
                //Stream stream = encoded.AsStream();
                // There is a bug so we cannot use stream here. Please refer to the below link about the issue.
                // https://github.cohttps://github.com/xamarin/Xamarin.Forms/issues/11495m/xamarin/Xamarin.Forms/issues/11495
                return ImageSource.FromStream(() => SKImage.FromPixels(bitmap.PeekPixels()).Encode().AsStream());
            }
            else { return null; }
        }

        public static SKBitmap GetBitmapByUrl(string url) 
        {
            try
            {
                string faviconUrl = RetrieveFavicon(url);
                Uri uri = new Uri(faviconUrl);
                WebClient myWebClient = new WebClient();
                byte[] pb = myWebClient.DownloadData(faviconUrl);

                return LoadImage(pb, faviconUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
            return null;
        }

        public static ImageSource GetImageByUrl(string url) 
        {
            SKBitmap bitmap = GetBitmapByUrl(url);

            try
            {
                if (bitmap != null) { return GetImageSource(bitmap); }
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"{ex}");
            }
            return null;
        }

        /// <summary>
        /// Extension method of KeePassLib.Item
        /// This method can be used to retrieve icon from a url.
        /// </summary>
        /// <param name="item">Instance of Item</param>
        /// <param name="url">This is the url using to retrieve icon.</param>
        public static void UpdateIcon(this Item item, string url)
        {
            var faviconUrl = RetrieveFavicon(url);

            try
            {
                var uri = new Uri(faviconUrl);
                WebClient myWebClient = new WebClient();
                byte[] pb = myWebClient.DownloadData(faviconUrl);

                SKBitmap bitmap = LoadImage(pb, faviconUrl);
                item.ImgSource = GetImageSource(bitmap);
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        public static void SetDefaultIcon(this Item item) 
        {
            var icon = new IconSource();

            if (item.IsGroup) 
            {
                icon.Icon = Icon.Folder;
            }
            else 
            {
                icon.Icon = Icon.File;
            }
            item.ImgSource = icon;
        }

        public static async Task SetCustomIconByUrl(this Item item, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) { return; }

            PasswordDb db = PasswordDb.Instance;
            try 
            {
                Uri uri = new Uri(url);
                PwCustomIcon old = db.GetCustomIcon(uri.Host);
                if (old == null)
                {
                    // If this is a new one, try to load it.
                    await Task.Run(() => AddNewIcon(item, url));
                }
                else
                {
                    // If an icon can be found, then use it.
                    item.CustomIconUuid = old.Uuid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        public static void SetIcon(this Item item)
        {
            // 1. Get built-in icon
            if (item.IsGroup)
            {
                // Group
                if (item is PwGroup group)
                {
                    if (group.CustomData.Exists(PxDefs.PxCustomDataIconName))
                    {
                        string iconPath = System.IO.Path.Combine(PxDataFile.IconFilePath, group.CustomData.Get(PxDefs.PxCustomDataIconName));
                        item.ImgSource = ImageSource.FromFile(iconPath);
                        return;
                    }
                }
            }
            else
            {
                // Entry
                if (item is PwEntry entry)
                {
                    if (entry.CustomData.Exists(PxDefs.PxCustomDataIconName))
                    {
                        string iconPath = System.IO.Path.Combine(PxDataFile.IconFilePath, entry.CustomData.Get(PxDefs.PxCustomDataIconName));
                        item.ImgSource = ImageSource.FromFile(iconPath);
                        return;
                    }
                }
            }

            // 2. Get custom icon
            if (item.CustomIconUuid != PwUuid.Zero)
            {
                PasswordDb db = PasswordDb.Instance;
                if(db != null)
                {
                    if(db.IsOpen)
                    {
                        PwCustomIcon customIcon = db.GetPwCustomIcon(item.CustomIconUuid);
                        if(customIcon != null) 
                        {
                            var pb = customIcon.ImageDataPng;
                            SKBitmap bitmap = LoadImage(pb);
                            item.ImgSource = GetImageSource(bitmap);
                            return;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("SetIcon: PasswordDb is closed");
                    }
                }
                else
                {
                    Debug.WriteLine("SetIcon: No PasswordDb instance");
                }
            }

            SetDefaultIcon(item);
        }

        /// <summary>
        /// Add a new custom icon to the database and set the new icon as the icon for this item.
        /// If the url is null, try to get the url from the URL field in the item.
        /// </summary>
        /// <param name="item">an instance of Item. Must not be <c>null</c>.</param>	
        /// <param name="url">Url used to retrieve the new icon.</param>	
		/// <returns>an instance of PxIcon</returns>
        public static PxIcon AddNewIcon(this Item item, string url = null)
        {
            PasswordDb db = PasswordDb.Instance;
            if (url == null && !item.IsGroup)
            {
                // If the url is null, we try to get the url from the URL field in the item.
                PwEntry entry = item as PwEntry;
                url = entry.GetUrlField();
            }

            if (db != null && !string.IsNullOrEmpty(url))
            {
                if (db.IsOpen)
                {
                    try
                    {
                        Uri uri = new Uri(url);
                        PwCustomIcon old = db.GetCustomIcon(uri.Host);
                        if (old == null)
                        {
                            SKBitmap bitmap = GetBitmapByUrl(url);
                            if (bitmap != null)
                            {
                                PwUuid uuid = db.SaveCustomIcon(bitmap, uri.Host);
                                if (!uuid.Equals(PwUuid.Zero))
                                {
                                    item.CustomIconUuid = uuid;

                                    PxIcon icon = new PxIcon
                                    {
                                        IconType = PxIconType.PxEmbeddedIcon,
                                        Uuid = uuid,
                                        Name = uri.Host
                                    };

                                    Debug.WriteLine($"AddNewIcon: hostname={uri.Host}");
                                    return icon;
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"AddNewIcon: Found an existing icon as {uri.Host}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{ex}");
                    }
                }
            }
            Debug.WriteLine("AddNewIcon: cannot add the new icon.");
            return null;
        }

        public static PxPlainFields GetPlainFields(this Item item)
        {
            return item.IsGroup ? ((PwGroup)item).GetPlainFields() : ((PwEntry)item).GetPlainFields();
        }

        public static bool IsNotes(this Item item)
        {
            return !item.IsGroup && ((PwEntry)item).IsNotes();
        }
    }
}
