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

    public partial class PianoPreview : UserControl
    {
        VelocityEase leftEase = new VelocityEase(0) { Duration = 0.1, Slope = 2, Supress = 2 };
        VelocityEase rightEase = new VelocityEase(128) { Duration = 0.1, Slope = 2, Supress = 2 };

        VelocityEase topEase = new VelocityEase(100) { Duration = 0.1, Slope = 2, Supress = 2 };
        VelocityEase bottomEase = new VelocityEase(0) { Duration = 0.1, Slope = 2, Supress = 2 };

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

        public PianoPreview()
        {
            InitializeComponent();

            CompositionTarget.Rendering += (s, e) =>
            {
                preview.ViewTop = topEase.GetValue();
                preview.ViewBottom = bottomEase.GetValue();
                preview.ViewLeft = leftEase.GetValue();
                preview.ViewRight = rightEase.GetValue();
            };
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
