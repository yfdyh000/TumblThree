using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using TumblThree.Applications.Extensions;
using TumblThree.Applications.Properties;

namespace TumblThree.Applications.Services
{
    [Export(typeof(IHttpRequestFactory))]
    public class WebRequestFactory : IHttpRequestFactory
    {
        private readonly IShellService shellService;
        private readonly ISharedCookieService cookieService;
        private readonly AppSettings settings;

        [ImportingConstructor]
        public WebRequestFactory(IShellService shellService, ISharedCookieService cookieService, AppSettings settings)
        {
            this.shellService = shellService;
            this.cookieService = cookieService;
            this.settings = settings;
        }
        private HttpClientHandler CreateHttpHandler()
        {
            HttpClientHandler httpHandler = new HttpClientHandler();
            
            httpHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; // def: None;
            httpHandler.AllowAutoRedirect = true;
            //httpHandler.CookieContainer = cookkie

            IWebProxy proxy = new WebProxy();
            if (!string.IsNullOrEmpty(settings.ProxyHost) && !string.IsNullOrEmpty(settings.ProxyPort))
            {
                proxy = new WebProxy(settings.ProxyHost, int.Parse(settings.ProxyPort));
                if (!string.IsNullOrEmpty(settings.ProxyUsername) && !string.IsNullOrEmpty(settings.ProxyPassword))
                    proxy.Credentials = new NetworkCredential(settings.ProxyUsername, settings.ProxyPassword);
            }
            httpHandler.Proxy = proxy;
            return httpHandler;
        }
        private HttpClient CreateHttpClient(HttpClientHandler httpHandler = null)
        {
            return new HttpClient(httpHandler ?? CreateHttpHandler())
            {
                //BaseAddress = new Uri(HttpUtility.UrlDecode(url)),
                Timeout = new TimeSpan(settings.TimeOut * 1000)
            };
        }
        private HttpRequestMessage CreateStubRequest(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var message = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Version = new Version("2.0")
            };
            message.Headers.Referrer = new Uri(referer);
            message.Headers.Add("User-Agent", settings.UserAgent);

            ServicePointManager.DefaultConnectionLimit = 400;
            headers = headers ?? new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> header in headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }

            return message;
        }

        public HttpRequestMessage CreateGetReqeust(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = CreateStubRequest(url, referer, headers);
            request.Method = HttpMethod.Get;
            return request;
        }

        public HttpRequestMessage CreateGetXhrReqeust(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = CreateStubRequest(url, referer, headers);
            request.Method = HttpMethod.Get;
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("appplication/json");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        public HttpRequestMessage CreatePostReqeust(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = CreateStubRequest(url, referer, headers);
            request.Method = HttpMethod.Post;
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            return request;
        }

        public HttpRequestMessage CreatePostXhrReqeust(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = CreatePostReqeust(url, referer, headers);
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpClient httpClient)
        {
            return httpClient.SendAsync(requestMessage);
        }

        public async Task PerformPostReqeustAsync(HttpRequestMessage request, Dictionary<string, string> parameters) //
        {
            string requestBody = UrlEncode(parameters);
            using (Stream postStream = await request.GetRequestStreamAsync().TimeoutAfter(shellService.Settings.TimeOut))
            {
                byte[] postBytes = Encoding.ASCII.GetBytes(requestBody);
                await postStream.WriteAsync(postBytes, 0, postBytes.Length);
                await postStream.FlushAsync();
            }
        }

        public async Task PerformPostXHRReqeustAsync(HttpRequestMessage request, string requestBody) //
        {
            using (Stream postStream = await request.GetRequestStreamAsync())
            {
                byte[] postBytes = Encoding.ASCII.GetBytes(requestBody);
                await postStream.WriteAsync(postBytes, 0, postBytes.Length);
                await postStream.FlushAsync();
            }
        }

        public async Task<bool> RemotePageIsValidAsync(string url)
        {
            var httpHandler = CreateHttpHandler();
            httpHandler.AllowAutoRedirect = false;
            var httpClient = CreateHttpClient(httpHandler);

            var request = CreateStubRequest(url);
            request.Method = HttpMethod.Head;
            var response = await httpClient.SendAsync(request);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public async Task<string> ReadReqestToEndAsync(HttpRequestMessage request) //
        {
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                using (Stream stream = GetStreamForApiRequest(response.GetResponseStream()))
                {
                    using (var buffer = new BufferedStream(stream))
                    {
                        using (var reader = new StreamReader(buffer))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public Stream GetStreamForApiRequest(Stream stream)
        {
            return !settings.LimitScanBandwidth || settings.Bandwidth == 0
                ? stream
                : new ThrottledStream(stream, (settings.Bandwidth / settings.ConcurrentConnections) * 1024);
        }

        public string UrlEncode(IDictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> val in parameters)
            {
                sb.AppendFormat("{0}={1}&", val.Key, HttpUtility.UrlEncode(val.Value));
            }

            sb.Remove(sb.Length - 1, 1); // remove last '&'
            return sb.ToString();
        }

        private static HttpRequestMessage SetWebRequestProxy(HttpRequestMessage request, AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.ProxyHost) && !string.IsNullOrEmpty(settings.ProxyPort))
            {
                request.Proxy = new WebProxy(settings.ProxyHost, int.Parse(settings.ProxyPort));
            }

            if (!string.IsNullOrEmpty(settings.ProxyUsername) && !string.IsNullOrEmpty(settings.ProxyPassword))
            {
                request.Proxy.Credentials = new NetworkCredential(settings.ProxyUsername, settings.ProxyPassword);
            }

            return request;
        }
    }
}
