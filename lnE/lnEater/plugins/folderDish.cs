using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using wnd = System.Windows.Forms;

namespace lnE
{
    public class LogicalComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }

    [DishAttribute(".*", Level = 1, DataFormat = "FileDrop")]
    public class folderDish : Dish
    {
        private List<string> files = new List<string>();
        private List<string> folders = new List<string>();

        public override List<Index> GetIndex(HtmlDocument html, string url, uint level, string path)
        {
            return null;
        }

        public override void Eat(HtmlDocument html, string url, string path)
        {

        }

        private void Do(List<string> list, string url, string path)
        {
            if (!list.Any())
                return;

            var title = Path.GetFileName(url);
            title = Path.ChangeExtension(title, ".txt");
            title = Path.Combine(path, title);

            list.Sort(new LogicalComparer());
            File.WriteAllText(title, String.Empty);
            foreach (var f in list)
            {
                File.AppendAllText(title, File.ReadAllText(f));
                File.AppendAllText(title, Enumerable.Range(1, 10).Select(i => Environment.NewLine).Aggregate((a, b) => a + b));
            }
        }

        public override HtmlDocument Load(string url, uint level, string path)
        {
            Do(files, url, path);

            foreach (var f in folders)
            {
                var list = Directory.GetFiles(f, "*.txt").ToList();
                Do(list, f, path);
            }

            return null;
        }

        public override Index GetLink(object rawData)
        {
            if (rawData == null)
                return null;

            var data = rawData as string[];
            if (data == null || data.Length == 0)
                return null;

            foreach (var item in data)
            {
                if (Directory.Exists(item))
                {
                    folders.Add(item);
                }
                else if (File.Exists(item) && Path.GetExtension(item) == ".txt")
                {
                    files.Add(item);
                }
            }

            var first = data.First();
            var file = Path.GetDirectoryName(first);
            return new Index { url = file, name = String.Empty };
        }
    }
}
