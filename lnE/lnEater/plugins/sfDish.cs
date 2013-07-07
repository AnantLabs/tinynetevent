using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace lnE
{
    [DishAttribute("http://book.sfacg.com/Novel/", Level = 1, Ext = ".txt")]
    public class sfDish : WebDish
    {
        public override List<Index> GetIndex(HtmlDocument html, string url, uint level, string path)
        {
            var books = new List<Index>();

            //var title = html.DocumentNode.SelectNodes("//body/div[2]/h1").First().InnerText;
            var toc = html.DocumentNode.SelectNodes("//div[@class='plate_top']").First().ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToArray();
            int sort = 1;
            for (int i = 0; i < toc.Length / 2; ++i)
            {
                int j = i * 2;
                var subTitle = toc[j].SelectNodes(".//span[1]").First().InnerText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).First();

                var list = toc[j + 1].SelectNodes(".//li");
                foreach (var chapter in list)
                {
                    var c = chapter.SelectNodes("a").First();
                    var n = String.Format("{2}_{0}-{1}", XTrim(subTitle), XTrim(c.InnerText), sort++);
                    var u = GetUrl(url, c.Attributes["href"].Value);
                    books.Add(new Index(level) { name = n, url = u });
                }
            }

            return books;
        }

        public override HtmlDocument Load(string url, uint level, string path)
        {
            if (level == 0)
            {
                url = new Uri(new Uri(url), "MainIndex/").AbsoluteUri;
            }

            return base.Load(url, level, path);
        }

        public override void Eat(HtmlDocument html, string url, string path)
        {
            var view = html.DocumentNode.SelectNodes("//span[@id='ChapterBody']");
            StringBuilder sb = new StringBuilder();
            foreach (var node in view.First().ChildNodes)
            {
                sb.Append(HtmlDecode(node.InnerText));
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}
