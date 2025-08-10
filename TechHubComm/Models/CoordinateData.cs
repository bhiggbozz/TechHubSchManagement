namespace TechHubComm.Models
{
    public class CoordinateData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float? PrevX { get; set; }
        public float? PrevY { get; set; }
        public List<PointF> Path { get; set; } = new();
    }

    public class DrawingProperties
    {
        public string Color { get; set; } = "#000000";
        public float StrokeWidth { get; set; } = 2.0f;
        public string Tool { get; set; } = "pen";
        public float Opacity { get; set; } = 1.0f;
    }

    public class PointF
    {
        public float X { get; set; }
        public float Y { get; set; }
    }
}
