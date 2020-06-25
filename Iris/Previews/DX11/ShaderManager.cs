using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris.Previews.DX11
{
    class ShaderManager : IDisposable
    {
        public ShaderBytecode vertexShaderByteCode;
        public VertexShader vertexShader;
        public ShaderBytecode pixelShaderByteCode;
        public PixelShader pixelShader;
        public ShaderBytecode geometryShaderByteCode;
        public GeometryShader geometryShader;

        public DisposeGroup disposer;

        public ShaderManager(Device device, ShaderBytecode vertexShaderByteCode, ShaderBytecode pixelShaderByteCode, ShaderBytecode geometryShaderByteCode)
        {
            disposer = new DisposeGroup();
            this.vertexShaderByteCode = disposer.Add(vertexShaderByteCode);
            this.pixelShaderByteCode = disposer.Add(pixelShaderByteCode);
            this.geometryShaderByteCode = disposer.Add(geometryShaderByteCode);
            vertexShader = disposer.Add(new VertexShader(device, vertexShaderByteCode));
            pixelShader = disposer.Add(new PixelShader(device, pixelShaderByteCode));
            geometryShader = disposer.Add(new GeometryShader(device, geometryShaderByteCode));
        }

        public void SetShaders(DeviceContext ctx)
        {
            ctx.VertexShader.Set(vertexShader);
            ctx.PixelShader.Set(pixelShader);
            ctx.GeometryShader.Set(geometryShader);
        }

        public void Dispose()
        {
            disposer.Dispose();
        }
    }
}
