using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inflectra.KronoDesk.Service.Email.Service.Helper_Classes
{
    /// <summary>
    /// Contains some helper utilities
    /// </summary>
    public static class Utils
    {
        /// <summary>Returns the filename extension for a specific MIME type</summary>
        /// <param name="mimeType">The MIME type</param>
        /// <returns>The file extension (e.g. image/gif returns just "gif")</returns>
        /// <remarks>Only used for Spira, since it doesn't have an API call to do this (unlike KronoDesk)</remarks>
        public static string GetExtensionFromMimeType(string mimeType)
        {
            string extension = "";

            if (string.IsNullOrEmpty(mimeType))
            {
                return "";
            }

            switch (mimeType.ToLowerInvariant())
            {
                case "application/illustrator":
                    extension = "ai";
                    break;
                case "application/postscript":
                    mimeType = "ps";
                    break;

                case "application/pdf":
                    extension = "pdf";
                    break;
                case "application/x-msexcel":
                    extension = "xls";
                    break;
                case "application/octet-stream":
                    extension = "msi";
                    break;
                case "application/x-mspowerpoint":
                    extension = "ppt";
                    break;
                case "text/plain":
                    extension = "txt";
                    break;
                case "application/vnd.visio":
                    extension = "vsd";
                    break;
                case "application/x-zip-compressed":
                    extension = "zip";
                    break;
                case "application/msword":
                    extension = "doc";
                    break;
                case "image/gif":
                    extension = "gif";
                    break;
                case "image/png":
                    extension = "png";
                    break;
                case "image/jpg":
                    extension = "jpeg";
                    break;
                case "text/xml":
                    extension = "xml";
                    break;
                case "image/bmp":
                    extension = "bmp";
                    break;
                case "image/photoshop":
                    extension = "psd";
                    break;
                case "application/vnd.ms-project":
                    extension = "mpp";
                    break;
                case "application/msaccess":
                    extension = "mdb";
                    break;
                case "application/msoutlook":
                    extension = "msg";
                    break;
                case "video/x-ms-wmv":
                    extension = "wmv";
                    break;
                case "audio/x-ms-wma":
                    extension = "wma";
                    break;

                case "text/html":
                    extension = "html";
                    break;
                case "image/jpeg":
                    extension = "jpeg";
                    break;
                case "image/tiff":
                    extension = "tiff";
                    break;
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                    extension = "docs";
                    break;
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.template":
                    extension = "dotx";
                    break;
                case "application/vnd.ms-word.template.macroEnabled.12":
                    extension = "dotm";
                    break;
                case "application/vnd.ms-word.document.macroEnabled.12":
                    extension = "docm";
                    break;
                case "application/vnd.openxmlformats-officedocument.presentationml.presentation":
                    extension = "pptx";
                    break;
                case "application/vnd.openxmlformats-officedocument.presentationml.slideshow":
                    extension = "ppsx";
                    break;
                case "application/vnd.ms-powerpoint.presentation.macroEnabled.12":
                    extension = "pptm";
                    break;
                case "application/vnd.ms-powerpoint.slideshow.macroEnabled.12":
                    extension = "ppsm";
                    break;
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                    extension = "xlsx";
                    break;
                case "application/vnd.ms-excel.sheet.binary.macroEnabled.12":
                    extension = "xlsb";
                    break;
                case "application/vnd.ms-excel.sheet.macroEnabled.12":
                    extension = "xlsm";
                    break;
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.template":
                    extension = "xltx";
                    break;
                case "application/vnd.ms-excel.template.macroEnabled.12":
                    extension = "xltm";
                    break;
                case "application/vnd.ms-excel.addin.macroEnabled.12":
                    extension = "xlam";
                    break;
            }

            return extension;
        }
    }
}
