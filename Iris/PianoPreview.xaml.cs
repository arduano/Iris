using MIDIModificationFramework;
using System;
using System.Collections;
using System.Collections.Generic;
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

namespace Iris
{
    public class ViewNoteSource : IEnumerable<TrackNote>
    {
        public ViewNoteSource(IEnumerable<TrackNote> source, int tracks)
        {
            Tracks = tracks;
            Source = source;
        }

        public int Tracks { get; }
        public IEnumerable<TrackNote> Source { get; }

        public IEnumerator<TrackNote> GetEnumerator()
        {
            return Source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Source.GetEnumerator();
        }
    }

    public class ViewRenderedArgs : EventArgs
    {
        public ViewRenderedArgs(Thickness renderWindow)
        {
            RenderWindow = renderWindow;
        }

        public Thickness RenderWindow { get; }
    }

    class ChildFetcher<T> : IDisposable where T : UIElement
    {
        Func<T> getNew;
        UIElementCollection collection;

        int count = 0;

        public T Get()
        {
            if (collection.Count <= count)
            {
                count++;
                var el = getNew();
                collection.Add(el);
                return el;
            }
            return (T)collection[count++];
        }

        public ChildFetcher(Func<T> getNew, UIElementCollection collection)
        {
            this.getNew = getNew;
            this.collection = collection;
        }

        public void Flush() => Dispose();

        public void Dispose()
        {
            if (count == -1) return;

            if (count < collection.Count)
                collection.RemoveRange(count, collection.Count - count);

            count = -1;
        }
    }

    public partial class PianoPreview : UserControl
    {
        VelocityEase leftEase = new VelocityEase(0) { Duration = 0.1, Slope = 2, Supress = 2 };
        VelocityEase rightEase = new VelocityEase(128) { Duration = 0.1, Slope = 2, Supress = 2 };

        VelocityEase topEase = new VelocityEase(100) { Duration = 0.1, Slope = 2, Supress = 2 };
        VelocityEase bottomEase = new VelocityEase(0) { Duration = 0.1, Slope = 2, Supress = 2 };

        public event EventHandler SourcingFinished;
        public event EventHandler<Exception> SourcingErrored;

        object sourceLock = new object();
        ViewNoteSource source;
        public ViewNoteSource ViewSource
        {
            get => source;
            set
            {
                lock (sourceLock)
                {
                    preview.StopSourcing();
                    source = value;
                    preview.SetNoteSource(source, source.Tracks);
                }
            }
        }

        Thickness lastDecoPos = new Thickness();

        bool VerticalChanged => preview.ViewTop != topEase.GetValue() || preview.ViewBottom != bottomEase.GetValue();
        bool HorizontalChanged => preview.ViewLeft != leftEase.GetValue() || preview.ViewRight != rightEase.GetValue();

        public PianoPreview()
        {
            InitializeComponent();

            preview.SourcingFinished += (s, e) => SourcingFinished?.Invoke(this, new EventArgs());
            preview.SourcingErrored += (s, e) => SourcingErrored?.Invoke(this, e);
            preview.RenderedFrame += Preview_RenderedFrame;

            containerGrid.SizeChanged += (s, e) => UpdatePos(lastDecoPos, true);

            CompositionTarget.Rendering += (s, e) =>
            {
                preview.ViewTop = topEase.GetValue();
                preview.ViewBottom = bottomEase.GetValue();
                preview.ViewLeft = leftEase.GetValue();
                preview.ViewRight = rightEase.GetValue();
            };
        }

        private void Preview_RenderedFrame(object sender, ViewRenderedArgs e)
        {
            if (e.RenderWindow == lastDecoPos) return;
            Dispatcher.Invoke(() =>
            {
                UpdatePos(e.RenderWindow);
            });
        }

        void UpdatePos(Thickness pos, bool force = false)
        {

            if (pos == lastDecoPos && !force) return;
            lastDecoPos = pos;

            var top = pos.Top;
            var bottom = pos.Bottom;

            var viewrange = top - bottom;

            if (viewrange == 0) return;
            using (var disp = new DisposeGroup())
            {
                var barLines = disp.Add(new ChildFetcher<Rectangle>(() => new Rectangle(), lineContainer.Children));
                var barLabels = disp.Add(new ChildFetcher<TextBlock>(() => new TextBlock() { 
                    LayoutTransform = new RotateTransform(90),
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Top,
                    TextAlignment = TextAlignment.Center,
                }, barNumbers.Children));

                double heightFromPos(double i) => lineContainer.ActualHeight + ((bottom - i) / viewrange) * lineContainer.ActualHeight;

                void addBar(double height, Brush brush)
                {
                    var line = barLines.Get();
                    line.Height = 2;
                    line.Fill = brush;
                    line.HorizontalAlignment = HorizontalAlignment.Stretch;
                    line.VerticalAlignment = VerticalAlignment.Top;
                    line.Margin = new Thickness(0, heightFromPos(height), 0, 0);
                }

                void addBarNumber(double height, int number)
                {
                    var text = barLabels.Get();
                    text.Width = 100;
                    text.Text = number.ToString();
                    text.FontSize = 16;
                    text.Margin = new Thickness(0, heightFromPos(height) - 50, 0, 0);
                }

                addBar(0, Brushes.Red);
                addBarNumber(0, 0);

                double interval = 1;
                while (viewrange / interval > 10) interval *= 4;

                for (double i = interval; i < top; i += interval)
                {
                    addBar(i, Brushes.Black);
                    addBarNumber(i, (int)Math.Round(i));
                }
            }
        }

        public bool AltScrolled { get; set; } = false;
        private void containerGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(containerGrid);
            var newTop = topEase.End;
            var newBotom = bottomEase.End;
            var newLeft = leftEase.End;
            var newRight = rightEase.End;

            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                var topDist = pos.Y / viewContainer.ActualHeight;
                var control = newTop + (newBotom - newTop) * topDist;
                var mult = Math.Pow(1.2, -e.Delta / 120);
                newTop = (newTop - control) * mult + control;
                newBotom = (newBotom - control) * mult + control;
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt))
            {
                AltScrolled = true;
                var leftDist = pos.X / viewContainer.ActualWidth;
                var control = newLeft + (newRight - newLeft) * leftDist;
                var mult = Math.Pow(1.2, -e.Delta / 120);
                newLeft = (newLeft - control) * mult + control;
                newRight = (newRight - control) * mult + control;
            }
            else
            {
                var height = newTop - newBotom;
                newTop += e.Delta / 120.0 / 10 * height;
                newBotom += e.Delta / 120.0 / 10 * height;
            }

            if (newRight > 128)
            {
                newLeft -= (newRight - 128);
                newRight = 128;
            }
            if (newLeft < 0)
            {
                newRight += (-newLeft);
                newLeft = 0;
            }
            if (newRight > 128)
            {
                newRight = 128;
            }

            //if (newTop > 1)
            //{
            //    newLeft -= (newRight - 1);
            //    newRight = 1;
            //}
            //if (newLeft < 0)
            //{
            //    newRight += (-newLeft);
            //    newLeft = 0;
            //}
            //if (newRight > 1)
            //{
            //    newRight = 1;
            //}

            if (newTop != topEase.End)
                topEase.SetEnd(newTop);
            if (newBotom != bottomEase.End)
                bottomEase.SetEnd(newBotom);

            if (newLeft != leftEase.End)
                leftEase.SetEnd(newLeft);
            if (newRight != rightEase.End)
                rightEase.SetEnd(newRight);
        }
    }
}
