﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using TumblThree.Applications.DataModels;
using TumblThree.Applications.DataModels.TumblrPosts;
using TumblThree.Applications.Parser;
using TumblThree.Applications.Properties;
using TumblThree.Applications.Services;
using TumblThree.Domain.Models.Blogs;

namespace TumblThree.Applications.Crawler
{
    public abstract class AbstractTumblrCrawler : AbstractCrawler
    {
        protected readonly IImgurParser imgurParser;
        protected readonly IGfycatParser gfycatParser;
        protected readonly IWebmshareParser webmshareParser;
        protected readonly IMixtapeParser mixtapeParser;
        protected readonly IUguuParser uguuParser;
        protected readonly ISafeMoeParser safemoeParser;
        protected readonly ILoliSafeParser lolisafeParser;
        protected readonly ICatBoxParser catboxParser;

        protected AbstractTumblrCrawler(IShellService shellService, ICrawlerService crawlerService, CancellationToken ct,
            PauseToken pt, IProgress<DownloadProgress> progress, IWebRequestFactory webRequestFactory,
            ISharedCookieService cookieService, IImgurParser imgurParser, IGfycatParser gfycatParser,
            IWebmshareParser webmshareParser, IMixtapeParser mixtapeParser, IUguuParser uguuParser, ISafeMoeParser safemoeParser,
            ILoliSafeParser lolisafeParser, ICatBoxParser catboxParser,
            IPostQueue<TumblrPost> postQueue, IBlog blog)
            : base(shellService, crawlerService, ct, pt, progress, webRequestFactory, cookieService, postQueue, blog)
        {
            this.imgurParser = imgurParser;
            this.gfycatParser = gfycatParser;
            this.webmshareParser = webmshareParser;
            this.mixtapeParser = mixtapeParser;
            this.uguuParser = uguuParser;
            this.safemoeParser = safemoeParser;
            this.lolisafeParser = lolisafeParser;
            this.catboxParser = catboxParser;
        }

        protected async Task<string> GetRequestAsync(string url)
        {
            var headers = new Dictionary<string, string>();
            string username = blog.Name + ".tumblr.com";
            string password = blog.Password;
            string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            headers.Add("Authorization", "Basic " + encoded);
            string[] cookieHosts = { "https://www.tumblr.com/" };
            return await RequestDataAsync(url, headers, cookieHosts);
        }

        protected async Task<string> UpdateTumblrKey(string url)
        {
            try
            {
                string document = await GetRequestAsync(url);
                return ExtractTumblrKey(document);
            }
            catch (TimeoutException timeoutException)
            {
                HandleTimeoutException(timeoutException, Resources.OnlineChecking);
                return string.Empty;
            }
        }

        protected static string ExtractTumblrKey(string document)
        {
            return Regex.Match(document, "id=\"tumblr_form_key\" content=\"([\\S]*)\">").Groups[1].Value;
        }

        /// <returns>
        ///     Return the url without the size and type suffix (e.g.
        ///     https://68.media.tumblr.com/51a99943f4aa7068b6fd9a6b36e4961b/tumblr_mnj6m9Huml1qat3lvo1).
        /// </returns>
        protected string GetCoreImageUrl(string url)
        {
            // return url.Split('_')[0] + "_" + url.Split('_')[1];
            return url;
        }

        protected string ImageSize()
        {
            return shellService.Settings.ImageSize == "raw" ? "1280" : shellService.Settings.ImageSize;
        }

        protected string ResizeTumblrImageUrl(string imageUrl)
        {
            var sb = new StringBuilder(imageUrl);
            return sb
                   .Replace("_raw", "_" + ImageSize())
                   .Replace("_1280", "_" + ImageSize())
                   .Replace("_540", "_" + ImageSize())
                   .Replace("_500", "_" + ImageSize())
                   .Replace("_400", "_" + ImageSize())
                   .Replace("_250", "_" + ImageSize())
                   .Replace("_100", "_" + ImageSize())
                   .Replace("_75sq", "_" + ImageSize())
                   .ToString();
        }

        protected void GenerateTags()
        {
            if (!string.IsNullOrWhiteSpace(blog.Tags))
            {
                tags = blog.Tags.Split(',').Select(x => x.Trim()).ToList();
            }
        }

        protected bool CheckIfSkipGif(string imageUrl)
        {
            return blog.SkipGif && imageUrl.EndsWith(".gif") || imageUrl.EndsWith(".gifv");
        }

        protected void AddWebmshareUrl(string post, string timestamp)
        {
            foreach (string imageUrl in webmshareParser.SearchForWebmshareUrl(post, blog.WebmshareType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new VideoPost(imageUrl, webmshareParser.GetWebmshareId(imageUrl),
                    timestamp));
            }
        }

        protected void AddMixtapeUrl(string post, string timestamp)
        {
            foreach (string imageUrl in mixtapeParser.SearchForMixtapeUrl(post, blog.MixtapeType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new ExternalVideoPost(imageUrl, mixtapeParser.GetMixtapeId(imageUrl),
                    timestamp));
            }
        }

        protected void AddUguuUrl(string post, string timestamp)
        {
            foreach (string imageUrl in uguuParser.SearchForUguuUrl(post, blog.UguuType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new ExternalVideoPost(imageUrl, uguuParser.GetUguuId(imageUrl),
                    timestamp));
            }
        }

        protected void AddSafeMoeUrl(string post, string timestamp)
        {
            foreach (string imageUrl in safemoeParser.SearchForSafeMoeUrl(post, blog.SafeMoeType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new ExternalVideoPost(imageUrl, safemoeParser.GetSafeMoeId(imageUrl),
                    timestamp));
            }
        }

        protected void AddLoliSafeUrl(string post, string timestamp)
        {
            foreach (string imageUrl in lolisafeParser.SearchForLoliSafeUrl(post, blog.LoliSafeType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new ExternalVideoPost(imageUrl, lolisafeParser.GetLoliSafeId(imageUrl),
                    timestamp));
            }
        }

        protected void AddCatBoxUrl(string post, string timestamp)
        {
            foreach (string imageUrl in catboxParser.SearchForCatBoxUrl(post, blog.CatBoxType))
            {
                if (CheckIfSkipGif(imageUrl))
                    continue;

                AddToDownloadList(new ExternalVideoPost(imageUrl, catboxParser.GetCatBoxId(imageUrl),
                    timestamp));
            }
        }
    }
}