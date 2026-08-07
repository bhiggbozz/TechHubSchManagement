namespace TechHub.Core.ViewModel.Attendance
{
	/// <summary>
	/// Payload sent when a teacher scans a student's QR code.
	/// The token is the value encoded inside the QR image.
	/// </summary>
	public class ScanQrCodeViewModel
	{
		public string QrToken { get; set; }
	}
}
