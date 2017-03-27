﻿using CloudFlareUtilities;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MangaRipper.Core.Services
{
    /// <summary>
    /// Support download web page to string and image file to folder.
    /// </summary>
    public class DownloadService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public CookieCollection Cookies { get; set; }

        public string Referrer { get; set; }
        
        private HttpClient CreateRequest()
        {
            var firstHandle = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                Credentials = CredentialCache.DefaultCredentials,
                AllowAutoRedirect = false,
                CookieContainer = new CookieContainer()
            };

            if (Cookies != null)
                firstHandle.CookieContainer.Add(Cookies);

            var cloudFlareHandler = new ClearanceHandler(firstHandle);
            var request = new HttpClient(cloudFlareHandler);
            if (Referrer != null)
                request.DefaultRequestHeaders.Referrer = new Uri(Referrer);

            // Add the Headers for "User Agent"
            return request;
        }

        /// <summary>
        /// Download single web page to string.
        /// </summary>
        /// <param name="url">The URL to download</param>
        /// <returns></returns>
        public async Task<string> DownloadStringAsync(string url)
        {
            Logger.Info("> DownloadStringAsync: {0}", url);
            return await WorkWithStreams(url);
        }

        /// <summary>
        /// Download a list of web page.
        /// </summary>
        /// <param name="urls">List of URL</param>
        /// <param name="progress">Progress report callback</param>
        /// <param name="cancellationToken">Cancellation control</param>
        /// <returns></returns>
        public async Task<string> DownloadStringAsync(IEnumerable<string> urls, IProgress<int> progress, CancellationToken cancellationToken)
        {
            var inputUrls = urls.ToArray();
            Logger.Info("> DownloadStringAsync(IEnumerable) - Total: {0}", inputUrls.Count());
            var sb = new StringBuilder();
            var count = 0;
            progress.Report(count);
            foreach (var url in inputUrls)
            {
                var input = await DownloadStringAsync(url);
                sb.Append(input);
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(count++);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Download file and save to folder
        /// </summary>
        /// <param name="url">The URL to download</param>
        /// <param name="fileName">Save to filename</param>
        /// <param name="cancellationToken">Cancellation control</param>
        /// <returns></returns>
        public async Task DownloadFileAsync(string url, string fileName, CancellationToken cancellationToken)
        {
            Logger.Info("> DownloadFileAsync begin: {0} - {1}", url, fileName);
            var result = await WorkWithStreams(url, fileName, cancellationToken);
            Logger.Info("> DownloadFileAsync result: {0} - {1}", url, result);
        }

        private async Task<string> WorkWithStreams(string url, string fileName = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var request = CreateRequest();

            using (var response = await request.GetAsync(url))
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    // Get the html from site
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    //Download the file
                    using (var streamReader = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        await response.Content.CopyToAsync(streamReader);
                        return response.StatusCode.ToString();
                    }
                }
            }
        }
    }
}
