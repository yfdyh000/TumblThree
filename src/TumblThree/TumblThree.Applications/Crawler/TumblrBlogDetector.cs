﻿using System;
using System.ComponentModel.Composition;
using System.Net;
using System.Threading.Tasks;

using TumblThree.Applications.Extensions;
using TumblThree.Applications.Services;

namespace TumblThree.Applications.Crawler
{
    [Export(typeof(ITumblrBlogDetector))]
    public class TumblrBlogDetector : ITumblrBlogDetector
    {
        private readonly IHttpRequestFactory webRequestFactory;
        private readonly IShellService shellService;
        protected readonly ISharedCookieService cookieService;

        [ImportingConstructor]
        public TumblrBlogDetector(IShellService shellService, ISharedCookieService cookieService,
            IHttpRequestFactory webRequestFactory)
        {
            this.webRequestFactory = webRequestFactory;
            this.cookieService = cookieService;
            this.shellService = shellService;
        }

        public async Task<bool> IsTumblrBlogAsync(string url)
        {
            string location = await GetUrlRedirection(url);
            return !location.Contains("login_required");
        }

        public async Task<bool> IsHiddenTumblrBlogAsync(string url)
        {
            string location = await GetUrlRedirection(url);
            return location.Contains("login_required") || location.Contains("dashboard/blog/");
        }

        public async Task<bool> IsPasswordProtectedTumblrBlogAsync(string url)
        {
            string location = await GetUrlRedirection(url);
            return location.Contains("blog_auth");
        }

        private async Task<string> GetUrlRedirection(string url)
        {
            HttpRequestMessage request = webRequestFactory.CreateGetReqeust(url);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            string location;
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                location = response.ResponseUri.ToString();
            }

            return location;
        }
    }
}
