﻿using ConfigCat.Cli.Models.Api;
using ConfigCat.Cli.Models.Configuration;
using ConfigCat.Cli.Services.Rendering;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trybot;

namespace ConfigCat.Cli.Services.Api
{
    public interface IOrganizationClient
    {
        Task<IEnumerable<OrganizationModel>> GetOrganizationsAsync(CancellationToken token);
    }

    public class OrganizationClient : ApiClient, IOrganizationClient
    {
        public OrganizationClient(IOutput output,
            CliConfig config,
            IBotPolicy<HttpResponseMessage> botPolicy,
            HttpClient httpClient)
            : base(output, config, botPolicy, httpClient)
        {
        }

        public Task<IEnumerable<OrganizationModel>> GetOrganizationsAsync(CancellationToken token) =>
            this.GetAsync<IEnumerable<OrganizationModel>>(HttpMethod.Get, "v1/organizations", token);
    }
}
