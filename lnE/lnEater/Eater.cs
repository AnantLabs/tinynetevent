using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using wnd = System.Windows.Forms;

namespace lnE
{
    public class Eater
    {
        private struct EDish
        {
            public string url;
            public string path;
            public Dish dish;
            public DishAttribute settings;
        }

        private static Assembly assembly;
        private static IList<Type> dishes;
        private IList<EDish> currentDishes;

        public static void Initialize(string path)
        {
            var dll = new[] { "HtmlAgilityPack.dll", "lnE.dll" };
            var references = ConfigurationManager.AppSettings["references"];
            if (!String.IsNullOrWhiteSpace(references))
            {
                dll = dll.Union(references.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            }
            assembly = AssemblyHelper.GetAssembly(dll, path);
            dishes = AssemblyHelper.GetTypes<DishAttribute>(assembly);
        }

        public bool Prepare(string url, string format)
        {
            currentDishes = SelectDishes(url, format);
            return currentDishes.Any();
        }

        public bool Prepare(wnd.IDataObject ido)
        {
            currentDishes = SelectDishes(ido);
            return currentDishes.Any();
        }

        private List<EDish> SelectDishes(string url, string format)
        {
            var list = (from dish in dishes
                        let s = dish.GetCustomAttribute<DishAttribute>()
                        where s.DataFormat == format && Regex.IsMatch(url, s.Pattern, RegexOptions.IgnoreCase)
                        select new EDish { url = url, path = String.Empty, dish = AssemblyHelper.GetObject<Dish>(dish), settings = s }).ToList();
            return list;
        }

        private List<EDish> SelectDishes(wnd.IDataObject ido)
        {
            var list = (from dish in dishes
                        let s = dish.GetCustomAttribute<DishAttribute>()
                        where ido.GetDataPresent(s.DataFormat)
                        let o = AssemblyHelper.GetObject<Dish>(dish)
                        let link = o.GetLink(ido.GetData(s.DataFormat))
                        where link != null && Regex.IsMatch(link.url, s.Pattern, RegexOptions.IgnoreCase)
                        select new EDish { url = link.url, path = link.name, dish = o, settings = s }).ToList();
            return list;
        }

        private async Task EatPage(EDish edish, string url, uint level, string path)
        {
            var doc = await Load(edish, url, level, path);

            if (doc == null)
                return;

            if (level > edish.settings.Level)
                throw new Exception("level");

            if (level == edish.settings.Level)
            {
                if (!String.IsNullOrWhiteSpace(edish.settings.Ext))
                    path = Path.ChangeExtension(path, edish.settings.Ext);
                edish.dish.Eat(doc, url, path);
                return;
            }

            await EatPage(edish, doc, url, level, path);
        }

        private async Task EatPage(EDish edish, HtmlDocument html, string url, uint level, string path)
        {
            var index = edish.dish.GetIndex(html, url, level, path);
            if (index == null)
                return;
            foreach (var i in index)
            {
                await EatPage(edish, i.url, i.level, Path.Combine(path, i.name));
            }
        }

        public string Name
        {
            get
            {
                if (!currentDishes.Any())
                    return String.Empty;

                return currentDishes.First().url;
            }
        }

        public async Task Eat(string path = null)
        {
            if (!currentDishes.Any())
                return;

            foreach (var edish in currentDishes)
            {
                if (String.IsNullOrWhiteSpace(path))
                {
                    path = edish.path;
                }
                else
                {
                    if (Path.IsPathRooted(edish.path))
                    {
                        path = edish.path;
                    }
                    else
                    {
                        path = Path.Combine(path, edish.path);
                    }
                }

                if (!String.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var doc = await Load(edish, edish.url, 0, path);
                if (doc == null)
                    continue;
                await EatPage(edish, doc, edish.url, 0, path);
            }
        }

        private async Task<HtmlDocument> Load(EDish edish, string url, uint level, string path)
        {
            return await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        return edish.dish.Load(url, level, path);
                    }
                    catch
                    {
                    }
                }
            });
        }
    }
}
