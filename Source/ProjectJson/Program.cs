namespace Microsoft.DotNet.Watcher
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoMapper;
    using Microsoft.DotNet.Cli.Utils;
    using Microsoft.DotNet.ProjectModel;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class Program : IDisposable
    {
        private const string ProjectJsonFileName = "project.json";
        private readonly FileSystemWatcher projectWatcher;
        private readonly FileSystemWatcher projectJsonWatcher;
        private readonly string projectDirectoryPath;
        private readonly string projectFilePath;
        private readonly string projectJsonFilePath;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        public Program()
        {
            this.projectDirectoryPath = Directory.GetCurrentDirectory();
            this.projectFilePath = Path.Combine(this.projectDirectoryPath, Project.FileName);
            this.projectJsonFilePath = Path.Combine(this.projectDirectoryPath, ProjectJsonFileName);
            this.jsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            this.projectWatcher = new FileSystemWatcher(this.projectDirectoryPath, Project.FileName)
            {
                EnableRaisingEvents = true
            };
            this.projectWatcher.Changed += this.OnProjectJsonChanged;
            this.projectJsonWatcher = new FileSystemWatcher(this.projectDirectoryPath, ProjectJsonFileName)
            {
                EnableRaisingEvents = true
            };
            this.projectJsonWatcher.Changed += this.OnProjectJsonChanged;
        }

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            using (CancellationTokenSource ctrlCTokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, ev) =>
                {
                    if (!ctrlCTokenSource.IsCancellationRequested)
                    {
                        Console.WriteLine($"project-json Shutdown requested. Press CTRL+C again to force exit.");
                        ev.Cancel = true;
                    }
                    else
                    {
                        ev.Cancel = false;
                    }
                    ctrlCTokenSource.Cancel();
                };

                try
                {
                    using (var program = new Program())
                    {
                        Console.Read();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        // swallow when only exception is the CTRL+C forced an exit
                        return 0;
                    }

                    Console.Error.WriteLine(ex.ToString());
                    Console.Error.WriteLine($"project-json An unexpected error occurred".Bold().Red());
                    return 1;
                }
            }

            return 0;
        }

        public void Dispose()
        {
            this.projectWatcher.Dispose();
            this.projectJsonWatcher.Dispose();
        }

        private void OnProjectChanged(object sender, FileSystemEventArgs e)
        {
            var project = ProjectReader.GetProject(this.projectFilePath);
            var json = JsonConvert.SerializeObject(project, this.jsonSerializerSettings);
            File.WriteAllText(this.projectJsonFilePath, json);
        }

        private void OnProjectJsonChanged(object sender, FileSystemEventArgs e)
        {
            var project = ProjectReader.GetProject(this.projectFilePath);
            var json = File.ReadAllText(this.projectJsonFilePath);
            var newProject = JsonConvert.DeserializeObject<Project>(json);
            Mapper.Map(newProject, project);
        }
    }
}