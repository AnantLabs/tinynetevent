using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace lnE
{
    [DishAttribute("http://www.ccmanhua.com/manhua/", Level = 2)]
    public class ccmanhuaDish : WebDish
    {
        public override List<Index> GetIndex(HtmlDocument html, string url, uint level, string path)
        {
            var books = new List<Index>();

            if (level == 1)
            {
                try
                {
                    var js = html.DocumentNode.SelectNodes("//body/script[1]").First().InnerText;
                    var ja = AssemblyHelper.EvalJs(js.Replace("getPageIndex()", "0"), "var a=[];a[0]=picTree,a[1]=pic_base;a");
                    var list = ja[0];
                    var site = ja[1];
                    for (int i = 0; i < list.length; ++i)
                    {
                        var u = site + list[i];
                        var ext = Path.GetExtension(new Uri(u).LocalPath);
                        var n = Path.ChangeExtension(i.ToString(), ext);
                        books.Add(new Index(level) { name = n, url = u });
                    }
                }
                catch
                {
                    var js = html.DocumentNode.SelectNodes("//script[1]").First().InnerText;
                    var ja = AssemblyHelper.EvalJs<string>(js, "qTcms_S_m_murl_e");
                    var result = Encoding.Default.GetString(Convert.FromBase64String(ja));
                    var list = result.Split(new[] { "$qingtiandy$" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < list.Length; ++i)
                    {
                        var u = list[i];
                        var n = Path.Combine(Path.DirectorySeparatorChar.ToString(), i.ToString(), Path.GetExtension(new Uri(u).LocalPath));
                        books.Add(new Index(level) { name = n, url = u });
                    }
                }
            }
            else
            {
                var toc = html.DocumentNode.SelectNodes("//div[@id='play_0']/ul/li");
                foreach (var t in toc)
                {
                    var link = t.SelectNodes(".//a").First();
                    var subTitle = link.Attributes["title"].Value;
                    var u = new Uri(new Uri(url, UriKind.Absolute), link.Attributes["href"].Value).AbsoluteUri;
                    var n = String.Format("{0}{1}", XTrim(subTitle), Path.DirectorySeparatorChar);

                    books.Add(new Index(level) { name = n, url = u });
                }
            }

            return books;
        }

        public override HtmlDocument Load(string url, uint level, string path)
        {
            if (level == 2)
            {
                var data = LoadData(url);
                var di = Path.GetDirectoryName(path);
                if (!Directory.Exists(di))
                    Directory.CreateDirectory(di);
                File.WriteAllBytes(path, data);
                return null;
            }

            return base.Load(url, level, path);
        }

        public override void Eat(HtmlDocument html, string url, string path)
        {
            
        }
    }
}
