﻿using ConfigCat.Cli.Models.Api;
using ConfigCat.Cli.Services;
using ConfigCat.Cli.Services.Api;
using ConfigCat.Cli.Services.Exceptions;
using ConfigCat.Cli.Services.Json;
using ConfigCat.Cli.Services.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigCat.Cli.Commands.Flags
{
    class Flag
    {
        private readonly IFlagClient flagClient;
        private readonly IConfigClient configClient;
        private readonly IProductClient productClient;
        private readonly IWorkspaceLoader workspaceLoader;
        private readonly IPrompt prompt;
        private readonly IOutput output;

        public Flag(IFlagClient flagClient,
            IConfigClient configClient,
            IProductClient productClient,
            IWorkspaceLoader workspaceLoader,
            IPrompt prompt,
            IOutput output)
        {
            this.flagClient = flagClient;
            this.configClient = configClient;
            this.productClient = productClient;
            this.workspaceLoader = workspaceLoader;
            this.prompt = prompt;
            this.output = output;
        }

        public async Task<int> ListAllFlagsAsync(string configId, string tagName, int? tagId, bool json, CancellationToken token)
        {
            var flags = new List<FlagModel>();
            if (!configId.IsEmpty())
                flags.AddRange(await this.flagClient.GetFlagsAsync(configId, token));
            else
            {
                var products = await this.productClient.GetProductsAsync(token);
                foreach (var product in products)
                {
                    var configs = await this.configClient.GetConfigsAsync(product.ProductId, token);
                    foreach (var config in configs)
                        flags.AddRange(await this.flagClient.GetFlagsAsync(config.ConfigId, token));
                }
            }

            if (!tagName.IsEmpty() || tagId is not null)
            {
                flags = flags.Where(f =>
                {
                    var result = true;
                    if (!tagName.IsEmpty())
                        result = f.Tags.Any(t => t.Name == tagName);

                    if (tagId is not null)
                        result = result && f.Tags.Any(t => t.TagId == tagId);

                    return result;
                }).ToList();
            }

            if (json)
            {
                this.output.RenderJson(flags);
                return ExitCodes.Ok;
            }

            var itemsToRender = flags.Select(f => new
            {
                Id = f.SettingId,
                f.Name,
                f.Key,
                Hint = f.Hint.TrimToFitColumn(),
                Type = f.SettingType,
                Tags = $"[{string.Join(", ", f.Tags.Select(t => $"{t.Name} ({t.TagId})"))}]",
                Owner = $"{f.OwnerUserFullName} [{f.OwnerUserEmail}]",
                Config = $"{f.ConfigName} [{f.ConfigId}]",
            });
            this.output.RenderTable(itemsToRender);

            return ExitCodes.Ok;
        }

        public async Task<int> CreateFlagAsync(string configId, CreateFlagModel createConfigModel, CancellationToken token)
        {
            var shouldPromptTags = configId.IsEmpty();

            if (configId.IsEmpty())
                configId = (await this.workspaceLoader.LoadConfigAsync(token)).ConfigId;

            if (createConfigModel.Name.IsEmpty())
                createConfigModel.Name = await this.prompt.GetStringAsync("Name", token);

            if (createConfigModel.Hint.IsEmpty())
                createConfigModel.Hint = await this.prompt.GetStringAsync("Hint", token);

            if (createConfigModel.Key.IsEmpty())
                createConfigModel.Key = await this.prompt.GetStringAsync("Key", token);

            if (createConfigModel.Type.IsEmpty())
                createConfigModel.Type = await this.prompt.ChooseFromListAsync("Choose type", SettingTypes.Collection.ToList(), t => t, token);

            if (shouldPromptTags && (createConfigModel.TagIds is null || !createConfigModel.TagIds.Any()))
                createConfigModel.TagIds = (await this.workspaceLoader.LoadTagsAsync(token, configId, optional: true)).Select(t => t.TagId);

            if (!SettingTypes.Collection.ToList()
                    .Contains(createConfigModel.Type, StringComparer.OrdinalIgnoreCase))
                throw new ShowHelpException($"Type must be one of the following: {string.Join('|', SettingTypes.Collection)}");

            var result = await this.flagClient.CreateFlagAsync(configId, createConfigModel, token);
            this.output.Write(result.SettingId.ToString());

            return ExitCodes.Ok;
        }

        public async Task<int> DeleteFlagAsync(int? flagId, CancellationToken token)
        {
            flagId ??= (await this.workspaceLoader.LoadFlagAsync(token)).SettingId;

            await this.flagClient.DeleteFlagAsync(flagId.Value, token);
            return ExitCodes.Ok;
        }

        public async Task<int> UpdateFlagAsync(int? flagId, UpdateFlagModel updateFlagModel, CancellationToken token)
        {
            var flag = flagId is null
                ? await this.workspaceLoader.LoadFlagAsync(token)
                : await this.flagClient.GetFlagAsync(flagId.Value, token);

            if (flagId is null)
            {
                if (updateFlagModel.Name.IsEmpty())
                    updateFlagModel.Name = await this.prompt.GetStringAsync("Name", token, flag.Name);

                if (updateFlagModel.Hint.IsEmpty())
                    updateFlagModel.Hint = await this.prompt.GetStringAsync("Hint", token, flag.Hint);

                if (updateFlagModel.TagIds is null || !updateFlagModel.TagIds.Any())
                    updateFlagModel.TagIds = (await this.workspaceLoader.LoadTagsAsync(token, flag.ConfigId, flag.Tags, optional: true)).Select(t => t.TagId);
            }

            var originalTagIds = flag.Tags?.Select(t => t.TagId)?.ToList() ?? new List<int>();

            if (updateFlagModel.Hint.IsEmptyOrEquals(flag.Hint) &&
                updateFlagModel.Name.IsEmptyOrEquals(flag.Name) &&
                (updateFlagModel.TagIds is null || 
                !updateFlagModel.TagIds.Any() || 
                updateFlagModel.TagIds.SequenceEqual(originalTagIds)))
            {
                this.output.WriteNoChange();
                return ExitCodes.Ok;
            }

            var updatedTagIds = updateFlagModel.TagIds.ToList();

            // prevent auto json patch generation for tag ids, we'll set them manually
            flag.Tags = null;
            updateFlagModel.TagIds = null;

            var patchDocument = JsonPatch.GenerateDocument(flag.ToUpdateModel(), updateFlagModel);

            var tagsToDelete = originalTagIds.Except(updatedTagIds).ToList();
            var tagsToAdd = updatedTagIds.Except(originalTagIds).ToList();

            var tagIndexes = new List<int>();
            foreach (var tagIndex in tagsToDelete.Select(deleteItem => originalTagIds.IndexOf(deleteItem)))
            {
                tagIndexes.Add(tagIndex);
                originalTagIds.RemoveAt(tagIndex);
            }

            foreach (var deleteIndex in tagIndexes)
                patchDocument.Remove($"/tags/{deleteIndex}");

            foreach (var tagIdToAdd in tagsToAdd)
                patchDocument.Add("/tags/-", tagIdToAdd);

            await this.flagClient.UpdateFlagAsync(flag.SettingId, patchDocument.Operations, token);
            return ExitCodes.Ok;
        }

        public async Task<int> AttachTagsAsync(int? flagId, IEnumerable<int> tagIds, CancellationToken token)
        {
            var flag = flagId is null
                ? await this.workspaceLoader.LoadFlagAsync(token)
                : await this.flagClient.GetFlagAsync(flagId.Value, token);

            var flagTagIds = flag.Tags.Select(t => t.TagId).ToList();

            if (flagId is null && tagIds is null || !tagIds.Any())
                tagIds = (await this.workspaceLoader.LoadTagsAsync(token, flag.ConfigId, flag.Tags)).Select(t => t.TagId);

            if (tagIds is null || 
                !tagIds.Any() || 
                tagIds.SequenceEqual(flagTagIds) ||
                !tagIds.Except(flagTagIds).Any())
            {
                this.output.WriteNoChange();
                return ExitCodes.Ok;
            }

            var patchDocument = new JsonPatchDocument();
            foreach (var tagId in tagIds)
                patchDocument.Add("/tags/-", tagId);

            await this.flagClient.UpdateFlagAsync(flag.SettingId, patchDocument.Operations, token);
            return ExitCodes.Ok;
        }

        public async Task<int> DetachTagsAsync(int? flagId, IEnumerable<int> tagIds, CancellationToken token)
        {
            var flag = flagId is null
                ? await this.workspaceLoader.LoadFlagAsync(token)
                : await this.flagClient.GetFlagAsync(flagId.Value, token);

            if (flagId is null && tagIds is null || !tagIds.Any())
                tagIds = (await this.prompt.ChooseMultipleFromListAsync("Choose tags to detach", flag.Tags, t => t.Name, token)).Select(t => t.TagId);

            var relevantTags = flag.Tags.Where(t => tagIds.Contains(t.TagId)).ToList();
            if (relevantTags.Count == 0)
            {
                this.output.WriteNoChange();
                return ExitCodes.Ok;
            }

            // play through the whole remove sequence as the indexes will change after each remove operation
            var tagIndexes = new List<int>();
            foreach (var index in relevantTags.Select(relevantTag => flag.Tags.IndexOf(relevantTag)))
            {
                tagIndexes.Add(index);
                flag.Tags.RemoveAt(index);
            }

            var patchDocument = new JsonPatchDocument();
            foreach (var tagIndex in tagIndexes)
                patchDocument.Remove($"/tags/{tagIndex}");

            await this.flagClient.UpdateFlagAsync(flag.SettingId, patchDocument.Operations, token);
            return ExitCodes.Ok;
        }
    }
}
