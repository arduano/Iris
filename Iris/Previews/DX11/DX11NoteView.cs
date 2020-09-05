using DX.WPF;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using IO = System.IO;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using SharpDX;
using System.Reflection;
using SharpDX.D3DCompiler;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using MIDIModificationFramework;
using System.Threading;
using System.Diagnostics;

namespace Iris.Previews.DX11
{
    public class DX11NoteView : ContentControl, INotePreview
    {
        bool cancelNoteFiller = false;
        Task noteFiller = null;
        List<TrackNote>[] noteArrays = new List<TrackNote>[128];

        Color4[] noteColorCache;

        bool[] blackKeys = new bool[257];
        int[] keynum = new int[257];
        int[] blackKeysID;
        int[] whiteKeysID;

        EventScene<D3D11> scene = new EventScene<D3D11>();

        D3D11 Renderer => scene.Renderer;

        public double ViewTop { get; set; }
        public double ViewBottom { get; set; }
        public double ViewLeft { get; set; }
        public double ViewRight { get; set; }

        System.Windows.Thickness previousThickness = new System.Windows.Thickness();

        public DX11NoteView()
        {
            ResetNoteArrays();

            scene.OnAttach += (s, e) => Attach();
            scene.OnRender += (s, e) => Render(e);
            scene.OnDetach += (s, e) => Detach();

            scene.Renderer = new D3D11() { SingleThreadedRender = false };

            Content = new DXElement() { Renderer = scene };

            for (int i = 0; i < blackKeys.Length; i++) blackKeys[i] = isBlackNote(i);
            int b = 0;
            int w = 0;
            List<int> black = new List<int>();
            List<int> white = new List<int>();
            for (int i = 0; i < keynum.Length; i++)
            {
                if (blackKeys[i])
                {
                    keynum[i] = b++;
                    if (i < 256)
                        black.Add(i);
                }
                else
                {
                    keynum[i] = w++;
                    if (i < 256)
                        white.Add(i);
                }
            }

            blackKeysID = black.ToArray();
            whiteKeysID = white.ToArray();
        }

        DisposeGroup disposer;
        ShaderManager notesShader;
        Buffer globalNoteConstants;
        InputLayout noteLayout;

        NotesGlobalConstants noteConstants;

        int noteBufferLength = 1 << 12;
        Buffer noteBuffer;

        void Attach()
        {
            disposer = new DisposeGroup();

            var device = Renderer.Device;

            string noteShaderData;
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("Iris.Previews.DX11.notes.fx"))
            using (var reader = new IO.StreamReader(stream))
                noteShaderData = reader.ReadToEnd();
            notesShader = disposer.Add(new ShaderManager(
                device,
                ShaderBytecode.Compile(noteShaderData, "VS_Note", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(noteShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(noteShaderData, "GS_Note", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            ));

            noteLayout = disposer.Add(new InputLayout(device, ShaderSignature.GetInputSignature(notesShader.vertexShaderByteCode), new[] {
                new InputElement("START",0,Format.R32_Float,0,0),
                new InputElement("END",0,Format.R32_Float,4,0),
                new InputElement("COLOR",0,Format.R32G32B32A32_Float,8,0),
            }));

            noteConstants = new NotesGlobalConstants()
            {
                NoteBorder = 0.002f,
                NoteLeft = -0.2f,
                NoteRight = 0.0f,
                ScreenAspect = 1f
            };

            noteBuffer = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 40 * noteBufferLength,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });

            globalNoteConstants = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = 32,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });

            var renderTargetDesc = new RenderTargetBlendDescription();
            renderTargetDesc.IsBlendEnabled = true;
            renderTargetDesc.SourceBlend = BlendOption.SourceAlpha;
            renderTargetDesc.DestinationBlend = BlendOption.InverseSourceAlpha;
            renderTargetDesc.BlendOperation = BlendOperation.Add;
            renderTargetDesc.SourceAlphaBlend = BlendOption.One;
            renderTargetDesc.DestinationAlphaBlend = BlendOption.One;
            renderTargetDesc.AlphaBlendOperation = BlendOperation.Add;
            renderTargetDesc.RenderTargetWriteMask = ColorWriteMaskFlags.All;

            BlendStateDescription desc = new BlendStateDescription();
            desc.AlphaToCoverageEnable = false;
            desc.IndependentBlendEnable = false;
            desc.RenderTarget[0] = renderTargetDesc;

            var blendStateEnabled = new BlendState(device, desc);

            device.ImmediateContext.OutputMerger.SetBlendState(blendStateEnabled);

            RasterizerStateDescription renderStateDesc = new RasterizerStateDescription
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                DepthBiasClamp = 0,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = false,
                IsFrontCounterClockwise = false,
                IsMultisampleEnabled = true,
                IsScissorEnabled = false,
                SlopeScaledDepthBias = 0
            };
            var rasterStateSolid = new RasterizerState(device, renderStateDesc);
            device.ImmediateContext.Rasterizer.State = rasterStateSolid;
        }

        void Detach()
        {
            disposer.Dispose();
        }

        int[] startNoteCache = new int[128];
        double lastBottomCache = 0;
        bool resetStartCache = false;

        public event EventHandler SourcingFinished;
        public event EventHandler<ViewRenderedArgs> RenderedFrame;
        public event EventHandler<Exception> SourcingErrored;

        void Render(DrawEventArgs args)
        {
            var device = Renderer.Device;
            var context = device.ImmediateContext;
            context.InputAssembler.InputLayout = noteLayout;
            var target = Renderer.RenderTargetView;

            notesShader.SetShaders(context);
            noteConstants.ScreenAspect = (float)(args.RenderSize.Height / args.RenderSize.Width);
            noteConstants.NoteBorder = 0.0015f;
            noteConstants.ScreenWidth = (int)args.RenderSize.Width;
            noteConstants.ScreenHeight = (int)args.RenderSize.Height;
            SetNoteShaderConstants(context, noteConstants);

            context.ClearRenderTargetView(target, new Color4(0.4f, 0.4f, 0.4f, 1f));
            //context.ClearRenderTargetView(target, new Color4(0.0f, 0.0f, 0.0f, 0f));

            double top = ViewTop;
            double bottom = ViewBottom;
            double range = top - bottom;

            lock (noteArrays)
            {
                if (ViewBottom < lastBottomCache)
                {
                    resetStartCache = true;
                }

                var viewLeft = ViewLeft;
                var viewRight = ViewRight;
                double viewRange = viewRight - viewLeft;

                Parallel.For(0, 128, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, k =>
                {
                    int pos = 0;


                    float left = (float)((k - viewLeft) / viewRange);
                    float right = (float)(((k + 1) - viewLeft) / viewRange);

                    unsafe
                    {
                        RenderNote* rn = stackalloc RenderNote[noteBufferLength];

                        void flush()
                        {
                            lock (context)
                            {
                                FlushNoteBuffer(
                                    context,
                                    left,
                                    right,
                                    (IntPtr)rn,
                                    pos
                                );
                                pos = 0;
                            }
                        }

                        var notes = noteArrays[k];

                        int start = 0;
                        if (resetStartCache)
                        {
                            for (; start < notes.Count; start++)
                            {
                                if (notes[start].End > bottom) break;
                            }
                            startNoteCache[k] = start;
                        }
                        else
                        {
                            start = startNoteCache[k];
                        }

                        if (left > 1 || right < 0)
                        {
                            return;
                        }
                        
                        for (int i = start; i < notes.Count; i++)
                        {
                            var n = notes[i];
                            if (n.End < bottom)
                            {
                                startNoteCache[k] = start;
                                continue;
                            }
                            if (n.Start > top) break;
                            rn[pos++] = new RenderNote()
                            {
                                color = noteColorCache[n.Track],
                                end = (float)((n.End - bottom) / range),
                                start = (float)((n.Start - bottom) / range),
                            };
                            if (pos >= noteBufferLength) flush();
                        }

                        flush();
                    }
                });

                var returnView = new System.Windows.Thickness(viewLeft, top, viewRight, bottom);
                RenderedFrame?.Invoke(this, new ViewRenderedArgs(previousThickness));
                previousThickness = returnView;

                resetStartCache = false;
                lastBottomCache = bottom;
            }
        }

        void SetNoteShaderConstants(DeviceContext context, NotesGlobalConstants constants)
        {
            DataStream data;
            context.MapSubresource(globalNoteConstants, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Write(constants);
            context.UnmapSubresource(globalNoteConstants, 0);
            context.VertexShader.SetConstantBuffer(0, globalNoteConstants);
            context.GeometryShader.SetConstantBuffer(0, globalNoteConstants);
            data.Dispose();
        }

        unsafe void FlushNoteBuffer(DeviceContext context, float left, float right, IntPtr notes, int count)
        {
            if (count == 0) return;
            DataStream data;
            context.MapSubresource(noteBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Position = 0;
            data.WriteRange(notes, count * sizeof(RenderNote));
            context.UnmapSubresource(noteBuffer, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(noteBuffer, 24, 0));
            noteConstants.NoteLeft = left;
            noteConstants.NoteRight = right;
            SetNoteShaderConstants(context, noteConstants);
            context.Draw(count, 0);
            data.Dispose();
        }

        unsafe void FlushNoteBuffer(DeviceContext context, float left, float right, RenderNote[] notes, int count)
        {
            if (count == 0) return;
            DataStream data;
            context.MapSubresource(noteBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Position = 0;
            data.WriteRange(notes, 0, count);
            context.UnmapSubresource(noteBuffer, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(noteBuffer, 24, 0));
            noteConstants.NoteLeft = left;
            noteConstants.NoteRight = right;
            SetNoteShaderConstants(context, noteConstants);
            context.Draw(count, 0);
            data.Dispose();
        }

        bool isBlackNote(int n)
        {
            n = n % 12;
            return n == 1 || n == 3 || n == 6 || n == 8 || n == 10;
        }

        void ResetNoteArrays()
        {
            lock (noteArrays)
            {
                for (int i = 0; i < 128; i++) noteArrays[i] = new List<TrackNote>();
            }
        }

        public void SetNoteSource(IEnumerable<TrackNote> notes, int trackCount)
        {
            IEnumerable<Color4> fetchColors()
            {
                for (int i = 0; i < trackCount; i++)
                {
                    yield return HsvToRgb(i * 0.3, 1, 1, 1);
                }
            }

            noteColorCache = fetchColors().ToArray();

            StopSourcing();

            noteFiller = Task.Run(() =>
            {
                Stopwatch s = new Stopwatch();
                s.Start();

                List<TrackNote>[] cache = new List<TrackNote>[128];

                void reset()
                {
                    for (int i = 0; i < 128; i++) cache[i] = new List<TrackNote>();
                }

                void flush()
                {
                    lock (noteArrays)
                    {
                        for (int i = 0; i < 128; i++)
                        {
                            noteArrays[i].AddRange(cache[i]);
                        }
                        resetStartCache = true;
                    }
                }

                reset();

                try
                {
                foreach (var n in notes)
                {
                    if (s.ElapsedMilliseconds > 1000 * 0.5)
                    {
                        flush();
                        reset();

                        s.Reset();
                        s.Start();
                    }
                    if (cancelNoteFiller) break;
                    cache[n.Key].Add(n);
                }
                }
                catch(Exception e)
                {
                    SourcingErrored?.Invoke(this, e);
                }
                flush();
                SourcingFinished?.Invoke(this, new EventArgs());
            });
        }

        public void StopSourcing()
        {
            if (noteFiller != null)
            {
                cancelNoteFiller = true;
                noteFiller.Wait();
                cancelNoteFiller = false;
            }
            ResetNoteArrays();
        }







        Color4 HsvToRgb(double h, double S, double V, double a)
        {
            int r, g, b;
            HsvToRgb(h * 360, S, V, out r, out g, out b);
            return new Color4(r / 255f, g / 255f, b / 255, (float)a);
        }

        void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }

        int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
