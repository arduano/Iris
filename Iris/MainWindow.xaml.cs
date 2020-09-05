using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    public enum ProjectState
    {
        None,
        Errored,
        Compiling,
        Building,
        Done,
    }

    public partial class MainWindow : Window
    {
        #region Dependency Properties

        async void Invoke(Action action)
        {
            if(Thread.CurrentThread != Dispatcher.Thread)
            {
                await Dispatcher.InvokeAsync(action);
            }
            else
            {
                action();
            }
        }

        public string CurrentProjectPath
        {
            get { return (string)GetValue(CurrentProjectPathProperty); }
            set { Invoke(() => SetValue(CurrentProjectPathProperty, value)); }
        }

        public static readonly DependencyProperty CurrentProjectPathProperty =
            DependencyProperty.Register(
                "CurrentProjectPath", 
                typeof(string), 
                typeof(MainWindow), 
                new PropertyMetadata("", (s, e) => (s as MainWindow).UpdateProjectFromPath((string)e.NewValue)));

        public string CompilerError
        {
            get { return (string)GetValue(CompilerErrorProperty); }
            set { Invoke(() => SetValue(CompilerErrorProperty, value)); }
        }

        public static readonly DependencyProperty CompilerErrorProperty =
            DependencyProperty.Register("CompilerError", typeof(string), typeof(MainWindow), new PropertyMetadata(""));

        public ProjectState ProjectViewState
        {
            get { return (ProjectState)GetValue(ProjectViewStateProperty); }
            set { Invoke(() => SetValue(ProjectViewStateProperty, value)); }
        }

        public static readonly DependencyProperty ProjectViewStateProperty =
            DependencyProperty.Register("ProjectViewState", typeof(ProjectState), typeof(MainWindow), new PropertyMetadata(ProjectState.None));

        #endregion

        void UpdateProjectFromPath(string path)
        {
            currentProject?.Dispose();

            try
            {
                currentProject = new ProjectFolder(path);
                currentProject.CompileEnded += CompileEnded;
                currentProject.CompileError += CompileError;
                currentProject.CompileStarted += CompileStarted;
                currentProject.RunCompiler();
            }
            catch (ArgumentException)
            {
                currentProject = null;
                CompilerError = "Project directory not found";
                ProjectViewState = ProjectState.Errored;
            }
        }

        ProjectFolder currentProject = null;

        private void CompileStarted(object sender, EventArgs e)
        {
            ProjectViewState = ProjectState.Compiling;
        }

        private void CompileError(object sender, string e)
        {
            Console.Write(e);
            CompilerError = e;
            ProjectViewState = ProjectState.Errored;
        }

        private void CompileEnded(object sender, IEnumerable<IEnumerable<MIDIEvent>> e)
        {
            CompilerError = null;
            var tracks = e.ToArray();
            var merged = tracks.Select((t, i) => t.ExtractNotes().ToTrackNotes(i).TaskedThreadedBuffer(10, 1000)).MergeAll();
            pianoPreview.ViewSource = new ViewNoteSource(merged, tracks.Length);
            ProjectViewState = ProjectState.Building;
        }

        private void Preview_SourcingFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (ProjectViewState == ProjectState.Building) ProjectViewState = ProjectState.Done;
            });
        }

        private void PianoPreview_SourcingErrored(object sender, Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                if (ProjectViewState == ProjectState.Building)
                {
                    ProjectViewState = ProjectState.Errored;
                    CompilerError = $"Errored while generating midi!\n\nError:\n{e.Message}";
                }
            });
        }

        public MainWindow()
        {
            InitializeComponent();

            new InplaceConverter<ProjectState, Visibility>(
                new BBinding(ProjectViewStateProperty, this),
                s => s == ProjectState.Errored ? Visibility.Visible : Visibility.Collapsed)
                .Set(previewOverlayError, VisibilityProperty);

            new InplaceConverter<ProjectState, Visibility>(
                new BBinding(ProjectViewStateProperty, this),
                s => s == ProjectState.Compiling ? Visibility.Visible : Visibility.Collapsed)
                .Set(previewOverlayCompiling, VisibilityProperty);

            new InplaceConverter<ProjectState, Visibility>(
                new BBinding(ProjectViewStateProperty, this),
                s => s == ProjectState.Building ? Visibility.Visible : Visibility.Collapsed)
                .Set(previewOverlayBuilding, VisibilityProperty);

            pianoPreview.SourcingFinished += Preview_SourcingFinished;
            pianoPreview.SourcingErrored += PianoPreview_SourcingErrored;

            projectPath.Text = Path.Combine(Environment.CurrentDirectory, "test");

            //CurrentProjectPath = Path.Combine(Environment.CurrentDirectory, "test");
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
