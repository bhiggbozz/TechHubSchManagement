namespace TechHub.Core.Interface;

/// <summary>
/// Resolves an <see cref="IImageGenerationAgent"/> by name.
/// The active provider comes from configuration
/// (ImageGeneration:Provider), keeping agent selection swappable and
/// isolated from business logic.
/// </summary>
public interface IImageGenerationAgentFactory
{
	/// <summary>
	/// Returns the configured default agent. Throws if the provider name in
	/// config has no registered implementation.
	/// </summary>
	IImageGenerationAgent GetAgent();

	/// <summary>
	/// Returns the agent registered under <paramref name="agentName"/>.
	/// </summary>
	IImageGenerationAgent GetAgent(string agentName);
}
