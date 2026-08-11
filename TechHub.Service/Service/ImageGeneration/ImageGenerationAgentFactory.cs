using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using TechHub.Core.Configuration;
using TechHub.Core.Interface;

namespace TechHub.Service.Service.ImageGeneration;

/// <summary>
/// Resolves an <see cref="IImageGenerationAgent"/> by provider name.
/// The active provider is read from ImageGeneration:Provider in config,
/// which is what makes the agent swappable without code changes.
/// </summary>
public class ImageGenerationAgentFactory : IImageGenerationAgentFactory
{
	private readonly IReadOnlyDictionary<string, IImageGenerationAgent> _agents;
	private readonly ImageGenerationSettings _settings;

	public ImageGenerationAgentFactory(
		IEnumerable<IImageGenerationAgent> agents,
		IOptions<ImageGenerationSettings> settings)
	{
		_settings = settings.Value;
		_agents = agents.ToDictionary(
			a => a.Name,
			a => a,
			StringComparer.OrdinalIgnoreCase);
	}

	public IImageGenerationAgent GetAgent()
		=> GetAgent(_settings.Provider);

	public IImageGenerationAgent GetAgent(string agentName)
	{
		if (string.IsNullOrWhiteSpace(agentName) ||
			!_agents.TryGetValue(agentName, out var agent))
		{
			throw new InvalidOperationException(
				$"No image-generation agent registered for provider '{agentName}'");
		}

		return agent;
	}
}
