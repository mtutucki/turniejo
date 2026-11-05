using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Teams.UploadTeamLogo;

namespace Turniejo.Teams.UnitTests.Teams
{
    public sealed class UploadTeamLogoCommandHandlerTests
    {
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];
        private static readonly byte[] WebP = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8];

        private readonly ITeamLogoStorage _storage = Substitute.For<ITeamLogoStorage>();
        private readonly UploadTeamLogoCommandHandler _handler;

        public UploadTeamLogoCommandHandlerTests()
        {
            _storage.SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => $"/uploads/team-logos/x.{ci.ArgAt<string>(1)}");
            _handler = new UploadTeamLogoCommandHandler(_storage);
        }

        public static TheoryData<byte[], string> Images => new()
        {
            { Png, "png" },
            { Jpeg, "jpg" },
            { WebP, "webp" },
        };

        [Theory]
        [MemberData(nameof(Images))]
        public async Task Handle_ShouldSaveImage_WithExtensionFromContent(byte[] content, string extension)
        {
            var result = await _handler.Handle(new UploadTeamLogoCommand(Guid.NewGuid(), content), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.LogoUrl.Should().Be($"/uploads/team-logos/x.{extension}");
            await _storage.Received(1).SaveAsync(content, extension, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRejectSvg()
        {
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

            var result = await _handler.Handle(new UploadTeamLogoCommand(Guid.NewGuid(), svg), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            await _storage.DidNotReceive().SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public void Validator_ShouldRejectFilesOverTheLimit()
        {
            var validator = new UploadTeamLogoCommandValidator();

            var tooBig = validator.Validate(new UploadTeamLogoCommand(Guid.NewGuid(), new byte[UploadTeamLogoCommand.MaxBytes + 1]));
            var empty = validator.Validate(new UploadTeamLogoCommand(Guid.NewGuid(), []));

            tooBig.IsValid.Should().BeFalse();
            empty.IsValid.Should().BeFalse();
        }
    }
}
