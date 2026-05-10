using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Board;

public class BoardBatchViewModel
{
	public string SessionId { get; set; }
	public string LessonId { get; set; }
	public int BatchIndex { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public int StrokeCount { get; set; }
	public int BoardIndex { get; set; }
	public List<StrokeViewModel> Strokes { get; set; } = new();
}

public class StrokeViewModel
{
	public string Id { get; set; }
	public List<List<double>> Pts { get; set; }  // [[x,y],...]
	public string C { get; set; }  // color
	public double W { get; set; }  // width
	public long Ts { get; set; }  // timestamp ms
	public int CurrentBoard { get; set; }
	public string SessionId { get; set; }
}
