using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using MIDIModificationFramework;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iris
{
    class CompileException : Exception
    {
        public CompileException(string message) : base(message) { }
    }

    class ProjectFolder : IDisposable
    {
        public string FolderPath { get; }

        string baseTypeName = "code";

        FileSystemWatcher watcher = new FileSystemWatcher();
        CSharpCodeProvider provider = new CSharpCodeProvider();

        public event EventHandler<IEnumerable<IEnumerable<MIDIEvent>>> CompileEnded;
        public event EventHandler<string> CompileError;
        public event EventHandler CompileStarted;

        Task currentHandler;
        CancellationTokenSource cancelTask;

        public ProjectFolder(string path)
        {
            if (!Directory.Exists(path)) throw new ArgumentException("Directory doesn't exist", "path");

            watcher = new FileSystemWatcher();
            provider = new CSharpCodeProvider();

            FolderPath = path;

            watcher.Path = FolderPath;
            watcher.NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName;
            watcher.Filter = "*.cs";

            watcher.Changed += (s, e) => RunCompiler();
            watcher.Created += (s, e) => RunCompiler();
            watcher.Deleted += (s, e) => RunCompiler();
            watcher.Renamed += (s, e) => RunCompiler();

            watcher.EnableRaisingEvents = true;
        }

        public void RunCompiler()
        {
            Interrupt();
            cancelTask = new CancellationTokenSource();
            var token = cancelTask.Token;
            currentHandler = Task.Run(() => RunCompilerInternal(token), token);
        }

        void Interrupt()
        {
            if (currentHandler?.IsCompleted == true) return;
            cancelTask?.Cancel();
            try
            {
                currentHandler?.Wait();
            }
            catch { }
        }

        object compileLock = new object();
        void RunCompilerInternal(CancellationToken cancel)
        {
            lock (compileLock)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    CompileStarted?.Invoke(this, new EventArgs());
                    var data = Compile(cancel).Generate();
                    cancel.ThrowIfCancellationRequested();
                    CompileEnded?.Invoke(this, data);
                }
                catch (CompileException e)
                {
                    cancel.ThrowIfCancellationRequested();
                    CompileError?.Invoke(this, e.Message);
                }
            }
        }

        IGenerator Compile(CancellationToken cancel)
        {
            try
            {
                cancel.ThrowIfCancellationRequested();

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

                var folder = Directory.GetFiles(FolderPath, "*.cs");

                cancel.ThrowIfCancellationRequested();
                if (folder.Length == 0) throw new CompileException("No code files found in this folder");

                string readFile(string path)
                {
                    using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var stream = new StreamReader(file))
                        return stream.ReadToEnd();
                }

                cancel.ThrowIfCancellationRequested();
                var text = folder.Select(t => readFile(t)).ToArray();

                cancel.ThrowIfCancellationRequested();
                CompilerResults results = provider.CompileAssemblyFromSource(compiler_parameters, text);

                if (results.Errors.HasErrors)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (CompilerError error in results.Errors)
                    {
                        builder.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));
                    }
                    string err = String.Format("Error on line {0}:\n{1}", results.Errors[0].Line, results.Errors[0].ErrorText) + "\n" + builder.ToString();
                    throw new CompileException(err);
                }

                var assembly = results.CompiledAssembly;

                var type = assembly.GetType(baseTypeName);
                if (type == null) throw new CompileException($"Base type with the name '{baseTypeName}' not found");
                var instance = Activator.CreateInstance(type);
                if (!(instance is IGenerator)) throw new CompileException($"Base type '{baseTypeName}' must implement the IGenerator interface");
                return instance as IGenerator;
            }
            catch (CompileException e)
            {
                throw e;
            }
            catch (OperationCanceledException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new CompileException("Compiler errored while compiling:\n" + e.Message);
            }
        }

        public void Dispose()
        {
            Interrupt();
            watcher.Dispose();
            provider.Dispose();
        }
    }
}
