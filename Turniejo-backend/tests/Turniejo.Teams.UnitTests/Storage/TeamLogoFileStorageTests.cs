using FluentAssertions;
using Turniejo.Teams.Infrastructure.Storage;

namespace Turniejo.Teams.UnitTests.Storage
{
    public sealed class TeamLogoFileStorageTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "turniejo-tests-" + Guid.NewGuid().ToString("N"));
        private readonly TeamLogoFileStorage _storage;

        public TeamLogoFileStorageTests() => _storage = new TeamLogoFileStorage(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Fact]
        public async Task SaveAsync_ShouldWriteFile_AndReturnPublicUrl()
        {
            var url = await _storage.SaveAsync([1, 2, 3], "png");

            url.Should().MatchRegex("^/uploads/team-logos/[0-9a-f]{32}\\.png$");
            var path = Path.Combine(_root, "team-logos", Path.GetFileName(url));
            (await File.ReadAllBytesAsync(path)).Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task DeleteIfStored_ShouldRemoveUploadedFile()
        {
            var url = await _storage.SaveAsync([1], "webp");

            _storage.DeleteIfStored(url);

            File.Exists(Path.Combine(_root, "team-logos", Path.GetFileName(url))).Should().BeFalse();
        }

        [Fact]
        public void DeleteIfStored_ShouldIgnoreForeignAndSuspiciousPaths()
        {
            var outside = Path.Combine(_root, "keep.png");
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(outside, [1]);

            _storage.DeleteIfStored("https://example.com/logo.png");
            _storage.DeleteIfStored("/uploads/team-logos/../keep.png");
            _storage.DeleteIfStored(null);

            File.Exists(outside).Should().BeTrue();
        }
    }
}
