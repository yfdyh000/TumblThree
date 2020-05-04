using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using TumblThree.Applications.Extensions;

namespace TumblThree.Applications.Services
{
    [Export]
    [Export(typeof(ILoginService))]
    internal class LoginService : ILoginService
    {
        private readonly IShellService shellService;
        private readonly ISharedCookieService cookieService;
        private readonly IHttpRequestFactory webRequestFactory;
        private string tumblrKey = string.Empty;
        private bool tfaNeeded = false;
        private string tumblrTFAKey = string.Empty;

        [ImportingConstructor]
        public LoginService(IShellService shellService, IHttpRequestFactory webRequestFactory, ISharedCookieService cookieService)
        {
            this.shellService = shellService;
            this.webRequestFactory = webRequestFactory;
            this.cookieService = cookieService;
        }

        public async Task PerformTumblrLoginAsync(string login, string password)
        {
            try
            {
                var document = await RequestTumblrKey().ConfigureAwait(false);
                if (document?.Length == 0) throw new TimeoutException();
                tumblrKey = ExtractTumblrKey(document);
                await Register(login, password).ConfigureAwait(false);
                document = await Authenticate(login, password).ConfigureAwait(false);
                if (tfaNeeded)
                {
                    tumblrTFAKey = ExtractTumblrTFAKey(document);
                }
            }
            catch { }
        }

        public void PerformTumblrLogout()
        {
            var cookieContainer = webRequestFactory.TakeHttpHandler.CookieContainer;

            var tosCookie = cookieContainer.GetCookies(new Uri("https://www.tumblr.com/"))["pfg"]; // pfg cookie contains ToS/GDPR agreement
            cookieService.RemoveUriCookie(cookieContainer, new Uri("https://www.tumblr.com"));
            cookieContainer.Add(tosCookie);
        }

        public bool CheckIfTumblrTFANeeded() => tfaNeeded;

        public async Task PerformTumblrTFALoginAsync(string login, string tumblrTFAAuthCode)
        {
            try
            {
                await SubmitTFAAuthCode(login, tumblrTFAAuthCode).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

        private static string ExtractTumblrKey(string document) => Regex.Match(document, "id=\"tumblr_form_key\" content=\"([\\S]*)\">").Groups[1].Value;

        private async Task<string> RequestTumblrKey()
        {
            try {
                const string url = "https://www.tumblr.com/login";
                var res = await webRequestFactory.GetReqeust(url);
                return await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                //if (ex is System.Net.Http.HttpRequestException ||
                //    ex is TimeoutException)
                return "";
            }
        }

        private async Task Register(string login, string password)
        {
            const string url = "https://www.tumblr.com/svc/account/register";
            const string referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            var request = webRequestFactory.PostXhrReqeustMessage(url, referer, headers);
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", string.Empty },
                { "user[password]", string.Empty },
                { "tumblelog[name]", string.Empty },
                { "user[age]", string.Empty },
                { "context", "no_referer" },
                { "version", "STANDARD" },
                { "follow", string.Empty },
                { "form_key", tumblrKey },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", string.Empty },
                { "tracking_url", "/login" },
                { "tracking_version", "modal" },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" },
            };
            await webRequestFactory.PostReqeustAsync(request, parameters);
        }

        private async Task<string> Authenticate(string login, string password)
        {
            const string url = "https://www.tumblr.com/login";
            const string referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            var request = webRequestFactory.PostReqeustMessage(url, referer, headers);
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", login },
                { "user[password]", password },
                { "tumblelog[name]", string.Empty },
                { "user[age]", string.Empty },
                { "context", "no_referer" },
                { "version", "STANDARD" },
                { "follow", string.Empty },
                { "form_key", tumblrKey },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", string.Empty },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" }
            };
            
            using (var response = await webRequestFactory.PostReqeustAsync(request, parameters))
            {
                if (response.Headers.Location == new Uri("https://www.tumblr.com/login")) // TFA required
                {
                    tfaNeeded = true;
                    return await response.Content.ReadAsStringAsync();
                }
                return string.Empty;
            }
        }

        private static string ExtractTumblrTFAKey(string document) => Regex.Match(document, "name=\"tfa_form_key\" value=\"([\\S]*)\"/>").Groups[1].Value;

        private async Task SubmitTFAAuthCode(string login, string tumblrTFAAuthCode)
        {
            const string url = "https://www.tumblr.com/login";
            const string referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            var request = webRequestFactory.PostReqeustMessage(url, referer, headers);
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", login },
                { "tumblelog[name]", string.Empty },
                { "user[age]", string.Empty },
                { "context", "login" },
                { "version", "STANDARD" },
                { "follow", string.Empty },
                { "form_key", tumblrKey },
                { "tfa_form_key", tumblrTFAKey },
                { "tfa_response_field", tumblrTFAAuthCode },
                { "http_referer", "https://www.tumblr.com/login" },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", string.Empty },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" }
            };
            await webRequestFactory.PostReqeustAsync(request, parameters);
        }

        public bool CheckIfLoggedIn()
        {
            return webRequestFactory.TakeHttpHandler.CookieContainer.GetCookieHeader(new Uri("https://www.tumblr.com/")).Contains("pfs");
        }

        public async Task<string> GetTumblrUsernameAsync()
        {
            const string tumblrAccountSettingsUrl = "https://www.tumblr.com/settings/account";
            var request = await webRequestFactory.GetReqeust(tumblrAccountSettingsUrl);
            var document = await request.Content.ReadAsStringAsync();
            return ExtractTumblrUsername(document);
        }

        private static string ExtractTumblrUsername(string document) => Regex.Match(document, "<p class=\"accordion_label accordion_trigger\">([\\S]*)</p>").Groups[1].Value;
    }
}
