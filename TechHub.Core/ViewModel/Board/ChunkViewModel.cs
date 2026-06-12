using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Board;

//public class ChunkViewModel
//{
//	[JsonPropertyName("index")]
//	public int Index { get; set; }

//	[JsonPropertyName("startMs")]
//	public long StartMs { get; set; }

//	[JsonPropertyName("endMs")]
//	public long EndMs { get; set; }

//	[JsonPropertyName("audio")]
//	public ChunkAudioViewModel Audio { get; set; } = new();

//	[JsonPropertyName("events")]
//	public List<ChunkEventViewModel> Events { get; set; } = new();  // ← typed
//}

public class ChunkAudioViewModel
{
	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	[JsonPropertyName("mediaId")]
	public string MediaId { get; set; } = string.Empty;

	[JsonPropertyName("sizeBytes")]
	public long SizeBytes { get; set; }

	[JsonPropertyName("durationMs")]
	public long DurationMs { get; set; }
}

public class StrokeBatchRefViewModel
{
	[JsonPropertyName("batchIndex")]
	public int BatchIndex { get; set; }

	[JsonPropertyName("indexKey")]
	public string IndexKey { get; set; } = string.Empty;

	[JsonPropertyName("startMs")]
	public long StartMs { get; set; }

	[JsonPropertyName("endMs")]
	public long EndMs { get; set; }

	[JsonPropertyName("strokeCount")]
	public int StrokeCount { get; set; }

	[JsonPropertyName("sizeBytes")]
	public long SizeBytes { get; set; }

	[JsonPropertyName("boardIndex")]
	public int BoardIndex { get; set; }  
}

