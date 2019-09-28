using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CueSharp;

namespace gdidrop
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Form1 form1 = new Form1();
            form1.ShowDialog();
        }
    }
}
