using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris
{
    static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var window = new MainWindow();
            window.ShowDialog();
        }
    }
}
