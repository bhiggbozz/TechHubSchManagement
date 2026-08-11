using System.Threading;
using System.Threading.Tasks;
using TechHub.Core.Model;

namespace TechHub.Core.Interface;

/// <summary>
/// Abstraction over an AI image-generation provider.
///
/// Implementations are registered in DI and resolved by name through
/// <see cref="IImageGenerationAgentFactory"/>, so switching providers is a
/// config change (ImageGeneration:Provider) plus (optionally) a new
/// implementation — the rest of the pipeline is untouched.
/// </summary>
public interface IImageGenerationAgent
{
	/// <summary>Unique agent name used as the config key, e.g. "Stability".</summary>
	string Name { get; }

	Task<ImageGenerationResult> GenerateAsync(
		ImageGenerationRequest request,
		CancellationToken cancellationToken = default);
}
