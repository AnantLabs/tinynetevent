using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace lnE
{
    [DishAttribute("http://www.bilibili.tv/video/", Level = 2, Ext = ".xml")]
    public class bilibiliDish : WebDish
    {
        public override List<Index> GetIndex(HtmlAgilityPack.HtmlDocument html, string url, uint level, string path)
        {
            var books = new List<Index>();

            if (level == 1)
            {
                books.Add(new Index(level) { name = path, url = GetTargetUrl(html) });
                return books;
            }

            var page = html.DocumentNode.SelectNodes("//body/div[@class='z']/div[@class='videobox']/div[@class='viewbox']").First();
            
            var title = page.SelectNodes("./div[@class='info']/h2").First().InnerText;
            var toc = page.SelectNodes("./div[@class='alist']/div[@id='alist']").First();
            if (toc.HasChildNodes)
            {
                foreach (var t in toc.SelectNodes(".//option"))
                {
                    var subTitle = t.InnerText;
                    var n = String.Format("[{0}]{1}", XTrim(title), XTrim(subTitle));
                    var u = t.Attributes["value"].Value;
                    books.Add(new Index(level) { name = n, url = u });
                }
            }
            
            if (!books.Any())
            {
                books.Add(new Index() { name = Path.Combine(path, XTrim(title)), url = GetTargetUrl(html), level = 2 });
            }
            else
            {
                var first = books.First();
                first.url = GetTargetUrl(html);
                first.level = 2;
            }

            return books;
        }

        private string GetTargetUrl(HtmlAgilityPack.HtmlDocument html)
        {
            var target = html.DocumentNode.SelectNodes("//body/div[@class='z']/div[@class='videobox']/div[@class='scontent']/iframe").FirstOrDefault();
            if (target == null)
                return null;

            var ex = new Regex("cid=(\\d+)");
            var m = ex.Match(target.Attributes["src"].Value);
            if (m.Groups.Count < 2)
                return null;

            var id = m.Groups[1].Value;

            return String.Format("http://comment.bilibili.tv/{0}.xml", id);
        }

        public override void Eat(HtmlAgilityPack.HtmlDocument html, string url, string path)
        {
            html.Save(path, Encoding.UTF8);
        }
    }
}
