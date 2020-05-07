using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using TumblThree.Applications.Properties;
using TumblThree.Applications.Services;
using TumblThree.Domain.Models;

namespace TumblThree.Applications.Parser
{
    public class GfycatParser : IGfycatParser
    {
        private readonly AppSettings settings;
        private readonly IHttpRequestFactory webRequestFactory;
        private readonly CancellationToken ct;

        public GfycatParser(AppSettings settings, IHttpRequestFactory webRequestFactory, CancellationToken ct)
        {
            this.settings = settings;
            this.webRequestFactory = webRequestFactory;
            this.ct = ct;
        }

        public Regex GetGfycatUrlRegex() => new Regex("(http[A-Za-z0-9_/:.]*gfycat.com/([A-Za-z0-9_]*))");

        public string GetGfycatId(string url) => GetGfycatUrlRegex().Match(url).Groups[2].Value;

        public virtual async Task<string> RequestGfycatCajax(string gfyId)
        {
                string url = @"https://gfycat.com/cajax/get/" + gfyId;
                var request = webRequestFactory.GetXhrReqeustMessage(url);
                var res = await webRequestFactory.SendAsync(request);
                return await res.Content.ReadAsStringAsync();
        }

        public string ParseGfycatCajaxResponse(string result, GfycatTypes gfycatType)
        {
            XmlDictionaryReader jsonReader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(result), new XmlDictionaryReaderQuotas());
            XElement root = XElement.Load(jsonReader);
            string url;
            switch (gfycatType)
            {
                case GfycatTypes.Gif:
                    url = root.Element("gfyItem").Element("gifUrl").Value;
                    break;
                case GfycatTypes.Max5mbGif:
                    url = root.Element("gfyItem").Element("max5mbGif").Value;
                    break;
                case GfycatTypes.Max2mbGif:
                    url = root.Element("gfyItem").Element("max2mbGif").Value;
                    break;
                case GfycatTypes.Mjpg:
                    url = root.Element("gfyItem").Element("mjpgUrl").Value;
                    break;
                case GfycatTypes.Mp4:
                    url = root.Element("gfyItem").Element("mp4Url").Value;
                    break;
                case GfycatTypes.Poster:
                    url = root.Element("gfyItem").Element("posterUrl").Value;
                    break;
                case GfycatTypes.Webm:
                    url = root.Element("gfyItem").Element("webmUrl").Value;
                    break;
                case GfycatTypes.Webp:
                    url = root.Element("gfyItem").Element("webpUrl").Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gfycatType));
            }

            return url;
        }

        public async Task<IEnumerable<string>> SearchForGfycatUrlAsync(string searchableText, GfycatTypes gfycatType)
        {
            var urlList = new List<string>();
            Regex regex = GetGfycatUrlRegex();
            foreach (Match match in regex.Matches(searchableText))
            {
                string gfyId = match.Groups[2].Value;
                urlList.Add(ParseGfycatCajaxResponse(await RequestGfycatCajax(gfyId), gfycatType));
            }

            return urlList;
        }
    }
}
