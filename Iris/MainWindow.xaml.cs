using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MIDIModificationFramework;
using Path = System.IO.Path;

namespace Iris
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.Combine(Environment.CurrentDirectory, "test");
            watcher.NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName;
            watcher.Filter = "*.cs";

            void runCompile()
            {
                IEnumerable<IEnumerable<MIDIEvent>> data;
                try
                {
                    data = Compile.Do();
                }
                catch { return; }
                var tracks = data.ToArray();
                var merged = tracks.Select((t, i) => t.ExtractNotes().ToTrackNotes(i).TaskedThreadedBuffer(10, 1000)).MergeAll();
                pianoPreview.ViewSource = new ViewNoteSource(merged, tracks.Length);
            }

            watcher.Changed += (s, e) => runCompile();
            watcher.Created += (s, e) => runCompile();
            watcher.Deleted += (s, e) => runCompile();
            watcher.Renamed += (s, e) => runCompile();

            watcher.EnableRaisingEvents = true;

            runCompile();
        }

        private void mainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (pianoPreview.AltScrolled)
            {
                e.Handled = true;
                pianoPreview.AltScrolled = false;
            }
        }
    }
}
