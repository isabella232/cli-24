﻿using ConfigCat.Cli.Models.Api;
using ConfigCat.Cli.Models.Configuration;
using ConfigCat.Cli.Services.Json;
using ConfigCat.Cli.Services.Rendering;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trybot;

namespace ConfigCat.Cli.Services.Api
{
    public interface IFlagClient
    {
        Task<IEnumerable<FlagModel>> GetFlagsAsync(string configId, CancellationToken token);

        Task<IEnumerable<DeletedFlagModel>> GetDeletedFlagsAsync(string configId, CancellationToken token);

        Task<FlagModel> GetFlagAsync(int flagId, CancellationToken token);

        Task<FlagModel> CreateFlagAsync(string configId, CreateFlagModel createFlagModel, CancellationToken token);

        Task UpdateFlagAsync(int flagId, List<JsonPatchOperation> operations, CancellationToken token);

        Task DeleteFlagAsync(int flagId, CancellationToken token);
    }

    public class FlagClient : ApiClient, IFlagClient
    {
        public FlagClient(IOutput output,
            CliConfig config,
            IBotPolicy<HttpResponseMessage> botPolicy,
            HttpClient httpClient)
            : base(output, config, botPolicy, httpClient)
        { }

        public Task<IEnumerable<FlagModel>> GetFlagsAsync(string configId, CancellationToken token) =>
            this.GetAsync<IEnumerable<FlagModel>>(HttpMethod.Get, $"v1/configs/{configId}/settings", token);

        public Task<IEnumerable<DeletedFlagModel>> GetDeletedFlagsAsync(string configId, CancellationToken token) =>
            this.GetAsync<IEnumerable<DeletedFlagModel>>(HttpMethod.Get, $"v1/configs/{configId}/deleted-settings", token);

        public Task<FlagModel> GetFlagAsync(int flagId, CancellationToken token) =>
            this.GetAsync<FlagModel>(HttpMethod.Get, $"v1/settings/{flagId}", token);

        public Task<FlagModel> CreateFlagAsync(string configId, CreateFlagModel createFlagModel, CancellationToken token) =>
            this.SendAsync<FlagModel>(HttpMethod.Post, $"v1/configs/{configId}/settings", createFlagModel, token);

        public async Task DeleteFlagAsync(int flagId, CancellationToken token)
        {
            this.Output.Write($"Deleting Flag... ");
            await this.SendAsync(HttpMethod.Delete, $"v1/settings/{flagId}", null, token);
            this.Output.WriteSuccess();
            this.Output.WriteLine();
        }

        public async Task UpdateFlagAsync(int flagId, List<JsonPatchOperation> operations, CancellationToken token)
        {
            this.Output.Write($"Updating Flag... ");
            await this.SendAsync(HttpMethod.Patch, $"v1/settings/{flagId}", operations, token);
            this.Output.WriteSuccess();
            this.Output.WriteLine();
        }
    }
}
