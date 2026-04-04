using ApartmentManagement.Utilities;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Utilities;

public sealed class UploadPathSecurityTests
{
    [Fact]
    public void AssertPathUnderRoot_Allows_File_Inside_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "upsec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var rootFull = UploadPathSecurity.NormalizeDirectory(root);
            var child = Path.Combine(rootFull, "avatars", "abc", "f.png");
            var childFull = Path.GetFullPath(child);
            var act = () => UploadPathSecurity.AssertPathUnderRoot(rootFull, childFull);
            act.Should().NotThrow();
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void AssertPathUnderRoot_Throws_When_Candidate_Outside_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "upsec2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var rootFull = UploadPathSecurity.NormalizeDirectory(root);
            var outside = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system.ini")
                : "/etc/hosts";
            var act = () => UploadPathSecurity.AssertPathUnderRoot(rootFull, outside);
            act.Should().Throw<InvalidOperationException>().WithMessage("*escapes*");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
