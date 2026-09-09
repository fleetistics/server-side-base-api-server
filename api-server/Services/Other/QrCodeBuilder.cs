using db_model.Media;
using QRCoder;

namespace api_server.Service
{
	public class QrCodeBuilder
	{
		public QrCodeBuilder(IConfiguration configuration, ILogger<QrCodeBuilder> logger)
		{
			mLogger = logger;
			var uploadFolder = configuration.GetValue<string>("Media:UploadFolder");
			if (string.IsNullOrEmpty(uploadFolder))
			{
				throw new InvalidOperationException("Media:UploadFolder configuration is missing or empty.");
			}
			mUploadFolder = uploadFolder;
		}

		public async Task<string> CreateQrCodeFileAsync(string content, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new Exception("Empty content to create QRCode");
			}

			string fileName = Guid.NewGuid().ToString() + QRCodeFileExt;
			string filePath = Path.Combine(mUploadFolder, fileName);

			using (var qrGenerator = new QRCodeGenerator())
			using (var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
			using (var qrCode = new PngByteQRCode(qrCodeData))
			{
				var imageData = qrCode.GetGraphic(QRCodePixelsPerModule);
				await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);
				mLogger.LogInformation($"Created QRCode file [{filePath}] mUploadFolder [{mUploadFolder}] fileName [{fileName}]");
			}
			return fileName;
		}
        public async Task<UploadedMedia> CreateQrCodeAsync(string content, CancellationToken cancellationToken)
        {
            return new UploadedMedia
            {
                FileName = await CreateQrCodeFileAsync(content, cancellationToken),
                MediaType = MediaType.Image
            };
        }
        private const string QRCodeFileExt = ".png";
		private const int QRCodePixelsPerModule = 5;

		private readonly ILogger<QrCodeBuilder> mLogger;
		private readonly string mUploadFolder;
	}
}
