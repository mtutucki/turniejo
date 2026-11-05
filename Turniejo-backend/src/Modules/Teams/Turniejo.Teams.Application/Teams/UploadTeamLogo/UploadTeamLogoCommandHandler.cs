using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.Teams.UploadTeamLogo
{
    internal sealed class UploadTeamLogoCommandHandler(ITeamLogoStorage storage)
        : ICommandHandler<UploadTeamLogoCommand, UploadTeamLogoResponse>
    {
        public async Task<Result<UploadTeamLogoResponse>> Handle(UploadTeamLogoCommand request, CancellationToken cancellationToken)
        {
            // Typ pliku po zawartości, nie po nazwie ani Content-Type od klienta. SVG celowo odpada:
            // może zawierać skrypt.
            var extension = DetectImageExtension(request.Content);
            if (extension == null)
                return Result.Failure<UploadTeamLogoResponse>(
                    Error.Validation("Content", "Logo must be a PNG, JPEG or WebP image"));

            var logoUrl = await storage.SaveAsync(request.Content, extension, cancellationToken);
            return Result.Success(new UploadTeamLogoResponse(logoUrl));
        }

        internal static string? DetectImageExtension(ReadOnlySpan<byte> content)
        {
            if (content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
                return "png";

            if (content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
                return "jpg";

            // RIFF....WEBP
            if (content.Length >= 12 &&
                content[..4].SequenceEqual("RIFF"u8) &&
                content.Slice(8, 4).SequenceEqual("WEBP"u8))
                return "webp";

            return null;
        }
    }
}
