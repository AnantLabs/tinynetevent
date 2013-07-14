using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace lnE
{
    public abstract class WebDish : Dish
    {
        protected string GetUrl(string baseUrl, string href)
        {
            Uri newUrl;
            if (!Uri.TryCreate(href, UriKind.Absolute, out newUrl))
            {
                newUrl = new Uri(new Uri(baseUrl, UriKind.Absolute), href);
            }
            return newUrl.AbsoluteUri;
        }

        protected string HtmlDecode(string html)
        {
            html = html.Replace("&lt;", "<");
            html = html.Replace("&gt;", ">");
            html = html.Replace("&nbsp;", " ");
            return html;
        }

        protected virtual bool BeforeRequest(WebClient client, string url)
        {
            client.Headers.Add(HttpRequestHeader.Referer, url);
            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/6.0)");
            //client.Headers.Add(HttpRequestHeader.Host, new Uri(url).Host);
            client.Headers.Add(HttpRequestHeader.Accept, "text/html, application/xhtml+xml, */*");
            client.Headers.Add(HttpRequestHeader.AcceptLanguage, "zh-CN");
            client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            client.Encoding = Encoding.UTF8;
            //client.Proxy = new WebProxy("127.0.0.1", 8888);

            return true;
        }

        protected virtual byte[] LoadData(string url)
        {
            var client = new WebClient();

            if (!BeforeRequest(client, url))
                return null;

            var data = client.DownloadData(url);

            var contentEncoding = client.ResponseHeaders["Content-Encoding"];
            bool needDecoding = false;
            Type decodingType = null;
            if (!String.IsNullOrWhiteSpace(contentEncoding))
            {
                var method = contentEncoding.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!String.IsNullOrWhiteSpace(method))
                {
                    if (method == "gzip")
                        decodingType = typeof(GZipStream);
                    else if (method == "deflate")
                        decodingType = typeof(DeflateStream);
                    else
                        throw new Exception("Not supported encoding " + method);
                    needDecoding = true;
                }
            }

            if (needDecoding)
            {
                using (var stream = new MemoryStream())
                using (var ms = new MemoryStream(data))
                using (var zip = (Stream)Activator.CreateInstance(decodingType, ms, CompressionMode.Decompress))
                {
                    byte[] buffer = new byte[4096];
                    int n = 0;
                    while ((n = zip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, n);
                    }
                    zip.Close();

                    data = stream.ToArray();
                    stream.Close();
                }
            }

            return data;
        }

        public override HtmlDocument Load(string url, uint level, string path)
        {
            var data = LoadData(url);
            if (data == null)
                return null;

            string html = Encoding.UTF8.GetString(data);

            var web = new HtmlDocument();
            web.LoadHtml(html);
            return web;
        }

        public override Index GetLink(object rawData)
        {
            string data = (string)rawData;
            if (data == null)
                return null;

            int begin = 0;
            int end = 0;
            string site = null;
            using (var reader = new StringReader(data))
            {
                do
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("StartFragment:"))
                    {
                        begin = Convert.ToInt32(line.Split(':').Skip(1).First().TrimStart('0'));
                    }
                    else if (line.StartsWith("EndFragment:"))
                    {
                        end = Convert.ToInt32(line.Split(':').Skip(1).First().TrimStart('0'));
                    }
                    else if (line.StartsWith("SourceURL:"))
                    {
                        site = line.Replace("SourceURL:", String.Empty);
                    }

                    if (begin != 0 && end != 0 && !String.IsNullOrWhiteSpace(site))
                        break;
                }
                while (reader.Peek() > 0);
            }

            var html = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(data).Skip(begin).Take(end - begin).ToArray());
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            try 
            {
                var node = doc.DocumentNode.SelectNodes("//a").First();
                var uri = new Uri(node.Attributes["href"].Value, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                {
                    var buri = new Uri(site, UriKind.Absolute);
                    Uri.TryCreate(buri, uri.OriginalString, out uri);
                    //buri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped);
                }

                var name = node.InnerText;
                if (String.IsNullOrWhiteSpace(name) && node.FirstChild != null)
                {
                    name = node.FirstChild.GetAttributeValue("alt", String.Empty);
                    if (String.IsNullOrWhiteSpace(name))
                        name = node.FirstChild.GetAttributeValue("title", String.Empty);
                    if (String.IsNullOrWhiteSpace(name))
                        name = node.InnerText;
                }

                return new Index { url = uri.AbsoluteUri, name = XTrim(name) };
            }
            catch
            {
                return null;
            }
        }
    }
}
