using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using wnd = System.Windows.Forms;

namespace lnE
{
    public class Index
    {
        public Index()
        {
            name = String.Empty;
            url = String.Empty;
            level = 0; 
        }

        public Index(uint curLevel)
        {
            name = String.Empty;
            url = String.Empty;
            level = curLevel + 1;  
        }

        public string name;
        public string url;
        public uint level;
    }

    public interface IDish
    {
        HtmlDocument Load(string url, uint level, string path);
        List<Index> GetIndex(HtmlDocument html, string url, uint level, string path);
        void Eat(HtmlDocument html, string url, string path);
        Index GetLink(object rawData);
    }

    public abstract class Dish : IDish
    {
        public abstract HtmlDocument Load(string url, uint level, string path);
        public abstract List<Index> GetIndex(HtmlDocument html, string url, uint level, string path);
        public abstract void Eat(HtmlDocument html, string url, string path);
        public abstract Index GetLink(object rawData);

        protected string XTrim(string str)
        {
            var s = str.Trim();
            s = s.Replace("\r", String.Empty);
            s = s.Replace("\n", String.Empty);
            s = s.Replace("\t", String.Empty);
            s = s.Replace(" ", "_");
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            foreach (var c in Path.GetInvalidPathChars())
                s = s.Replace(c, '_');
            s = s.Replace('.', '_');
            return s.Replace("&nbsp;", "_");
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DishAttribute : Attribute
    {
        public DishAttribute(string pattern)
        {
            DataFormat = wnd.DataFormats.Html;
            Pattern = pattern;
            Level = 1;
            Ext = String.Empty;
            Comment = String.Empty;
        }

        public string DataFormat
        {
            get;
            set;
        }

        public string Pattern
        {
            get;
            set;
        }

        public uint Level
        {
            get;
            set;
        }

        public string Ext
        {
            get;
            set;
        }

        public string Comment
        {
            get;
            set;
        }
    }
}
