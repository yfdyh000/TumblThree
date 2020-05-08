using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TumblThree.Applications.Properties;

namespace TumblThree.Applications.Services
{
    [Export(typeof(IHttpRequestFactory))]
    public class HttpRequestFactory : IHttpRequestFactory
    {
        //private readonly IShellService shellService;
        //private readonly ISharedCookieService cookieService;
        private readonly AppSettings settings;
        private HttpClientHandler httpHandler;
        private HttpClient httpClient;

        [ImportingConstructor]
        public HttpRequestFactory(IShellService shellService, ISharedCookieService cookieService, AppSettings settings, HttpClientHandler httpHandler, HttpClient httpClient)
        {
            //this.shellService = shellService;
            //this.cookieService = cookieService;
            this.settings = settings;
            this.httpHandler = httpHandler;
            this.httpClient = httpClient;

            //CreateHttpHandler();
            //CreateHttpClient();
        }

        //public HttpClientHandler gethttpHandler() { get { return httpHandler; }; set{ httpHandler = value;} };
        //public HttpClient gethttpClient() { };

        public HttpClientHandler TakeHttpHandler()
        {
            //var httpHandler = new HttpClientHandler();

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
        public HttpClient TakeHttpClient()
        {
            this.httpClient.Timeout = new TimeSpan(settings.TimeOut * 1000);
            return this.httpClient;
        }
        private HttpRequestMessage NewStubRequest(string url, string referer = "", Dictionary<string, string> headers = null)
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

        public async Task<HttpRequestMessage> GetReqeustMessage(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = NewStubRequest(url, referer, headers);
            request.Method = HttpMethod.Get;
            return request;
        }
        public async Task<HttpResponseMessage> GetReqeust(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            // TODO: try catch
            var request = NewStubRequest(url, referer, headers);
            request.Method = HttpMethod.Get;
            return await httpClient.SendAsync(request);
        }

        public HttpRequestMessage GetXhrReqeustMessage(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = NewStubRequest(url, referer, headers);
            request.Method = HttpMethod.Get;
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("appplication/json");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        public HttpRequestMessage PostReqeustMessage(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = NewStubRequest(url, referer, headers);
            request.Method = HttpMethod.Post;
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            return request;
        }

        public HttpRequestMessage PostXhrReqeustMessage(string url, string referer = "", Dictionary<string, string> headers = null)
        {
            var request = PostReqeustMessage(url, referer, headers);
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage)//, HttpClient httpClient)
        {
            // TODO: try catch
            return httpClient.SendAsync(requestMessage);
        }

        public async Task<HttpResponseMessage> PostReqeustAsync(HttpRequestMessage request, Dictionary<string, string> parameters) //
        {
            request.Content = new FormUrlEncodedContent(parameters);
            return await TakeHttpClient().SendAsync(request);
            
            //string requestBody = UrlEncode(parameters);
            //var res = await CreateHttpClient().SendAsync(request);

            /*var stringContent = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");
            return await CreateHttpClient().PostAsync(request.RequestUri, stringContent);*/

            /*res.Content
            using (Stream postStream = await request.GetRequestStreamAsync().TimeoutAfter(60))
            {
                byte[] postBytes = Encoding.ASCII.GetBytes(requestBody);
                await postStream.WriteAsync(postBytes, 0, postBytes.Length);
                await postStream.FlushAsync();
            }*/
        }

        public async Task<HttpResponseMessage> PostXHRReqeustAsync(HttpRequestMessage request, string requestBody)
        {
            request.Content = new StringContent(requestBody);
            return await TakeHttpClient().SendAsync(request);
        }

        public async Task<bool> RemotePageIsValidAsync(string url)
        {
            var httpHandler = TakeHttpHandler();
            httpHandler.AllowAutoRedirect = false;
            var httpClient = TakeHttpClient();

            var request = NewStubRequest(url);
            request.Method = HttpMethod.Head;
            var response = await httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
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
    }
}
