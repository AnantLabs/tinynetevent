using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace lnE
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Eater.Initialize("plugins");
        }

        private async void Form1_DragDrop(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;

            var eater = new Eater();
            if (eater.Prepare(e.Data))
            {
                await eater.Eat(ConfigurationManager.AppSettings["path"]);
                MessageBox.Show("Completed!");
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private void Form1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private async void Form1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.V))
            {
                var data = Clipboard.GetDataObject();
                if (data == null)
                    return;

                if (!data.GetDataPresent(typeof(string)))
                    return;

                var url = (string)data.GetData(typeof(string));

                try 
                {
                    new Uri(url);
                }
                catch
                {
                    return;
                }

                var eater = new Eater();
                if (eater.Prepare(url, DataFormats.Html))
                {
                    await eater.Eat(ConfigurationManager.AppSettings["path"]);
                    MessageBox.Show("Completed!");
                }

                //foreach (var format in data.GetFormats())
                //{
                //    var content = data.GetData(format);
                //}
            }
        }
    }
}
