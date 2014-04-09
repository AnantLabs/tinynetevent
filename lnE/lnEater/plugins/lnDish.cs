using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace lnE
{
    [DishAttribute("http://lknovel.lightnovel.cn/main/vollist/", Level = 1, Ext=".txt")]
    public class lnDish : WebDish
    {
        public override List<Index> GetIndex(HtmlDocument html, string url, uint level, string path)
        {
            var books = new List<Index>();

            var page = html.DocumentNode.SelectNodes("//body/div[3]/div[@class='container']").First();
            var title = page.SelectNodes(".//li[@class='active']").First().InnerText;
            var toc = page.SelectNodes(".//dl[last()]/*");
            int sort = 1;
            foreach (var t in toc)
            {
                var subTitle = t.SelectNodes(".//div[2]/h2//a").First().InnerText;

                var list = t.SelectNodes(".//div[2]/ul/li");
                if (list == null)
                    continue;
                foreach (var chapter in list)
                {
                    var c = chapter.SelectNodes("a").First();
                    var n = String.Format("{3}_[{0}]{1}-{2}", XTrim(title), XTrim(subTitle), XTrim(c.Element("span").InnerText), sort++);
                    var u = c.Attributes["href"].Value;
                    books.Add(new Index(level) { name = n, url = u });
                }
            }

            return books;
        }

        public override void Eat(HtmlDocument html, string url, string path)
        {
            var view = html.DocumentNode.SelectNodes("//div[@id='J_view']");
            StringBuilder sb = new StringBuilder();
            foreach (var node in view.Last().ChildNodes)
            {
                sb.Append(node.InnerText);
            }
            
            File.WriteAllText(path, sb.ToString());
        }
    }
}
