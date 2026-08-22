namespace TechhubMS.Pages;

using System.Text;

/// <summary>
/// Self-contained HTML for the one-time school logo upload page served at
/// GET /api/School/logo-setup/{schoolId} (e.g. https://green.bluetsch.com/api/School/logo-setup/d0d1c4f5-...).
/// </summary>
public static class SchoolLogoSetupPage
{
	private const string PageCss = @"
		* { margin: 0; padding: 0; box-sizing: border-box; }
		body { font-family: 'Segoe UI', Arial, sans-serif; background: #f0f4f8; min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
		.card { background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,.08); max-width: 480px; width: 100%; padding: 40px; text-align: center; }
		h1 { font-size: 22px; color: #1a2b3c; margin-bottom: 8px; }
		.sub { color: #64748b; font-size: 14px; margin-bottom: 28px; }
		.dropzone { border: 2px dashed #cbd5e1; border-radius: 10px; padding: 36px 16px; cursor: pointer; transition: all .2s; color: #64748b; font-size: 14px; }
		.dropzone:hover, .dropzone.dragover { border-color: #2563eb; background: #eff6ff; }
		.dropzone .icon { font-size: 40px; display: block; margin-bottom: 10px; }
		.preview { display: none; margin: 0 auto 20px; max-width: 220px; max-height: 160px; object-fit: contain; border-radius: 8px; }
		button { background: #2563eb; color: #fff; border: none; border-radius: 8px; padding: 12px 28px; font-size: 15px; cursor: pointer; margin-top: 20px; }
		button:disabled { background: #93c5fd; cursor: not-allowed; }
		button.secondary { background: transparent; color: #2563eb; border: 1px solid #2563eb; }
		.banner { padding: 14px; border-radius: 8px; font-size: 14px; margin-top: 18px; display: none; text-align: left; word-break: break-word; }
		.banner.error { background: #fef2f2; color: #b91c1c; border: 1px solid #fecaca; }
		.banner.success { background: #f0fdf4; color: #15803d; border: 1px solid #bbf7d0; }
		.logo-shown { max-width: 200px; max-height: 140px; object-fit: contain; margin: 20px auto; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,.1); }
		.spinner { display: inline-block; width: 16px; height: 16px; border: 2px solid #fff; border-top-color: transparent; border-radius: 50%; animation: spin .7s linear infinite; vertical-align: middle; margin-right: 8px; }
		@keyframes spin { to { transform: rotate(360deg); } }";

	public static string BuildNotFound() => Wrap($@"
			<h1>School not found</h1>
			<p class=""sub"">No active school matches this link.
			Check the link from your approval email or contact TechHub support.</p>");

	public static string BuildAlreadyUploaded(string schoolName, string logoUrl) => Wrap($@"
			<h1>{System.Net.WebUtility.HtmlEncode(schoolName)}</h1>
			<p class=""sub"">Your school logo is already set up.</p>
			<img class=""logo-shown"" src=""{System.Net.WebUtility.HtmlEncode(logoUrl)}"" alt=""School logo"" />
			<div class=""banner success"" style=""display:block;"">Logo uploaded. You can change it later from the school dashboard.</div>");

	public static string BuildUploadForm(Guid schoolId, string schoolName)
	{
		var jsIdentifier = schoolId.ToString();

		return Wrap($@"
			<h1>{System.Net.WebUtility.HtmlEncode(schoolName)}</h1>
			<p class=""sub"">Upload your official school logo to finish setting up your profile.</p>
			<img id=""preview"" class=""preview"" alt=""Logo preview"" />
			<div id=""dropzone"" class=""dropzone"">
				<span class=""icon"">&#128247;</span>
				Drag &amp; drop your logo here, or click to browse<br />
				<small>JPEG, PNG, WebP or SVG &middot; max 2MB</small>
				<input type=""file"" id=""fileInput"" accept="".jpg,.jpeg,.png,.webp,.svg,image/jpeg,image/png,image/webp,image/svg+xml"" hidden />
			</div>
			<button id=""uploadBtn"" disabled>Upload logo</button>
			<button id=""changeBtn"" class=""secondary"" style=""display:none;"">Choose another file</button>
			<div id=""banner"" class=""banner""></div>

			<script>
				(function () {{
					var identifier = '{jsIdentifier}';
					var dropzone = document.getElementById('dropzone');
					var fileInput = document.getElementById('fileInput');
					var preview = document.getElementById('preview');
					var uploadBtn = document.getElementById('uploadBtn');
					var changeBtn = document.getElementById('changeBtn');
					var banner = document.getElementById('banner');
					var selectedFile = null;

					function showBanner(kind, message) {{
						banner.className = 'banner ' + kind;
						banner.style.display = 'block';
						banner.textContent = message;
					}}

					function selectFile(file) {{
						if (!file) return;
						if (file.size > 2 * 1024 * 1024) {{ showBanner('error', 'Logo must be less than 2MB.'); return; }}
						selectedFile = file;
						preview.src = URL.createObjectURL(file);
						preview.style.display = 'block';
						dropzone.style.display = 'none';
						changeBtn.style.display = 'inline-block';
						uploadBtn.disabled = false;
						banner.style.display = 'none';
					}}

					function reset() {{
						selectedFile = null;
						fileInput.value = '';
						preview.style.display = 'none';
						dropzone.style.display = 'block';
						changeBtn.style.display = 'none';
						uploadBtn.disabled = true;
						banner.style.display = 'none';
					}}

					dropzone.addEventListener('click', function () {{ fileInput.click(); }});
					fileInput.addEventListener('change', function () {{ selectFile(this.files[0]); }});
					changeBtn.addEventListener('click', reset);

					['dragenter', 'dragover'].forEach(function (evt) {{
						dropzone.addEventListener(evt, function (e) {{ e.preventDefault(); dropzone.classList.add('dragover'); }});
					}});
					['dragleave', 'drop'].forEach(function (evt) {{
						dropzone.addEventListener(evt, function (e) {{ e.preventDefault(); dropzone.classList.remove('dragover'); }});
					}});
					dropzone.addEventListener('drop', function (e) {{ selectFile(e.dataTransfer.files[0]); }});

					uploadBtn.addEventListener('click', function () {{
						if (!selectedFile) return;
						uploadBtn.disabled = true;
						uploadBtn.innerHTML = '<span class=""spinner""></span>Uploading...';

						var form = new FormData();
						form.append('logo', selectedFile);

						fetch('/api/School/logo-setup/' + encodeURIComponent(identifier), {{ method: 'POST', body: form }})
							.then(function (res) {{ return res.json().then(function (data) {{ return {{ ok: res.ok, data: data }}; }}); }})
							.then(function (result) {{
								if (result.ok && result.data.status === 'successful') {{
									showBanner('success', result.data.responseMessage || 'Logo uploaded successfully.');
									uploadBtn.style.display = 'none';
									changeBtn.style.display = 'none';
								}} else if (result.data.responseCode === '99161') {{
									showBanner('error', result.data.responseMessage || 'A logo has already been uploaded.');
									uploadBtn.style.display = 'none';
									changeBtn.style.display = 'none';
								}} else {{
									showBanner('error', result.data.responseMessage || 'Upload failed. Please try again.');
									uploadBtn.innerHTML = 'Upload logo';
									uploadBtn.disabled = !selectedFile;
								}}
							}})
							.catch(function () {{
								showBanner('error', 'Network error. Please try again.');
								uploadBtn.innerHTML = 'Upload logo';
								uploadBtn.disabled = !selectedFile;
							}});
					}});
				}})();
			</script>");
	}

	private static string Wrap(string innerHtml)
	{
		var sb = new StringBuilder();
		sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" />");
		sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
		sb.Append("<title>TechHub — School Logo Setup</title>");
		sb.Append("<style>").Append(PageCss).Append("</style></head><body>");
		sb.Append("<div class=\"card\">");
		sb.Append(innerHtml);
		sb.Append("</div></body></html>");
		return sb.ToString();
	}
}
