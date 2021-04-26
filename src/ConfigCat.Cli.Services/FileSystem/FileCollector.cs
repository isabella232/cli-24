﻿using ConfigCat.Cli.Services.FileSystem.Ignore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigCat.Cli.Services.FileSystem
{
    public interface IFileCollector
    {
        Task<IEnumerable<FileInfo>> CollectAsync(DirectoryInfo rootDirectory, CancellationToken token);
    }

    public class FileCollector : IFileCollector
    {
        private readonly IExecutionContextAccessor executionContextAccessor;

        public FileCollector(IExecutionContextAccessor executionContextAccessor)
        {
            this.executionContextAccessor = executionContextAccessor;
        }

        public async Task<IEnumerable<FileInfo>> CollectAsync(DirectoryInfo rootDirectory, CancellationToken token)
        {
            var output = this.executionContextAccessor.ExecutionContext.Output;
            using var spinner = output.CreateSpinner(token);

            var files = rootDirectory.GetFiles("*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            });
            var ignoreFiles = files.Where(f => f.IsIgnoreFile());
            var filesToReturn = files.Except(ignoreFiles);
            var ignores = ignoreFiles.Select(f => new IgnoreFile(f, rootDirectory)).ToList();

            foreach (var ignore in ignores)
            {
                output.Verbose($"Using ignore file {ignore.File.FullName}");
                await ignore.LoadIgnoreFileAsync(token);
            }

            return filesToReturn.Where(f =>
            {
                foreach (var ignore in ignores.Where(i => i.Handles(f)).OrderByDescending(i => i.Rank))
                {
                    if (ignore.IsAccepting(f))
                        return true;

                    if (ignore.IsIgnoring(f))
                        return false;
                }

                return true;
            });
        }
    }
}
