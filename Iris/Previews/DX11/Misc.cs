using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Iris.Previews.DX11
{

    [StructLayout(LayoutKind.Sequential)]
    public struct NoteCol
    {
        public uint rgba;
        public uint rgba2;

        public static uint Compress(byte r, byte g, byte b, byte a)
        {
            return (uint)((r << 24) & 0xff000000) |
                       (uint)((g << 16) & 0xff0000) |
                       (uint)((b << 8) & 0xff00) |
                       (uint)(a & 0xff);
        }

        public static uint Blend(uint from, uint with)
        {
            Vector4 fromv = new Vector4((float)(from >> 24 & 0xff) / 255.0f, (float)(from >> 16 & 0xff) / 255.0f, (float)(from >> 8 & 0xff) / 255.0f, (float)(from & 0xff) / 255.0f);
            Vector4 withv = new Vector4((float)(with >> 24 & 0xff) / 255.0f, (float)(with >> 16 & 0xff) / 255.0f, (float)(with >> 8 & 0xff) / 255.0f, (float)(with & 0xff) / 255.0f);

            float blend = withv.W;
            float revBlend = (1 - withv.W) * fromv.W;

            return Compress(
                    (byte)((fromv.X * revBlend + withv.X * blend) * 255),
                    (byte)((fromv.Y * revBlend + withv.Y * blend) * 255),
                    (byte)((fromv.Z * revBlend + withv.Z * blend) * 255),
                    (byte)((blend + revBlend) * 255)
                );
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    struct NotesGlobalConstants
    {
        public float NoteLeft;
        public float NoteRight;
        public float NoteBorder;
        public float ScreenAspect;
        public float KeyboardHeight;
        public int ScreenWidth;
        public int ScreenHeight;
    }


    [StructLayout(LayoutKind.Sequential)]
    struct RenderNote
    {
        public float start;
        public float end;
        public Color4 color;
    }
}
