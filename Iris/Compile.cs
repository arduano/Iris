using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using MIDIModificationFramework;

namespace Iris
{
    public static class Compile
    {
        public static IEnumerable<IEnumerable<MIDIEvent>> Do()
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();

            CompilerParameters compiler_parameters = new CompilerParameters();

            compiler_parameters.GenerateInMemory = true;

            compiler_parameters.GenerateExecutable = false;

            compiler_parameters.ReferencedAssemblies.Add(typeof(object).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(MIDIEvent).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(IEnumerable<object>).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(LinkedList<object>).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(System.Drawing.Color).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(System.Linq.Enumerable).Assembly.Location);
            compiler_parameters.ReferencedAssemblies.Add(typeof(IGenerator).Assembly.Location);

            compiler_parameters.CompilerOptions = "/optimize /unsafe /nostdlib";

            var folder = Directory.GetFiles("test", "*.cs");

            string readFile(string path)
            {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var stream = new StreamReader(file))
                    return stream.ReadToEnd();
            }

            var text = folder.Select(t => readFile(t)).ToArray();

            CompilerResults results = provider.CompileAssemblyFromSource(compiler_parameters, text);

            if (results.Errors.HasErrors)
            {
                StringBuilder builder = new StringBuilder();
                foreach (CompilerError error in results.Errors)
                {
                    builder.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));
                }
                string err = String.Format("Error on line {0}:\n{1}", results.Errors[0].Line, results.Errors[0].ErrorText) + "\n" + builder.ToString();
                Console.WriteLine(err);
                throw new Exception(err);
            }

            var assembly = results.CompiledAssembly;

            var code = (IGenerator)Activator.CreateInstance(assembly.GetType("code"));

            return code.Generate();
        }
    }
}
