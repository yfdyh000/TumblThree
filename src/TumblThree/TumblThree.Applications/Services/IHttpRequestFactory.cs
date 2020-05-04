using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace TumblThree.Applications.Services
{
    public interface IHttpRequestFactory
    {
        HttpClientHandler CreateHttpHandler();
        HttpClient CreateHttpClient(HttpClientHandler httpHandler = null);
        HttpRequestMessage CreateGetReqeust(string url, string referer = "", Dictionary<string, string> headers = null);

        HttpRequestMessage CreateGetXhrReqeust(string url, string referer = "", Dictionary<string, string> headers = null);

        HttpRequestMessage CreatePostReqeust(string url, string referer = "", Dictionary<string, string> headers = null);

        HttpRequestMessage CreatePostXhrReqeust(string url, string referer = "", Dictionary<string, string> headers = null);

        Task PerformPostReqeustAsync(HttpRequestMessage request, Dictionary<string, string> parameters);

        Task PerformPostXHRReqeustAsync(HttpRequestMessage request, string requestBody);

        Task<bool> RemotePageIsValidAsync(string url);

        Task<string> ReadReqestToEndAsync(HttpRequestMessage request);

        Stream GetStreamForApiRequest(Stream stream);

        string UrlEncode(IDictionary<string, string> parameters);
    }
}
