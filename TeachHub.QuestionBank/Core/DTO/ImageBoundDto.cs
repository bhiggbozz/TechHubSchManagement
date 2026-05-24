using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.DTO;
public class ImageBoundDto
{
	public string Key { get; set; } = string.Empty;
	public int X { get; set; }
	public int Y { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
}
