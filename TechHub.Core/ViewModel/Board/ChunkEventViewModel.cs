using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Board;

public class ChunkEventViewModel
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("timestampMs")]
	public long TimestampMs { get; set; }

	[JsonPropertyName("mediaAssetId")]
	public string MediaAssetId { get; set; } = string.Empty;
}
