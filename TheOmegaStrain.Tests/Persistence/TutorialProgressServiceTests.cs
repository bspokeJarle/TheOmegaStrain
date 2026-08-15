using CommonUtilities.Persistence;

namespace _3DSpesificsUnitTests.Persistence;

[TestClass]
public class TutorialProgressServiceTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainTutorialProgressTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        try
        {
            if (Directory.Exists(_testLocalFolder))
                Directory.Delete(_testLocalFolder, recursive: true);
        }
        catch
        {
        }
    }

    [TestMethod]
    public void TutorialProgress_IsStoredPerPlayerLocally()
    {
        Assert.IsFalse(TutorialProgressService.HasCompletedTutorial("Jarle"));
        Assert.IsFalse(TutorialProgressService.HasCompletedTutorial("Anna"));

        TutorialProgressService.MarkTutorialCompleted("Jarle");

        Assert.IsTrue(TutorialProgressService.HasCompletedTutorial("Jarle"));
        Assert.IsFalse(TutorialProgressService.HasCompletedTutorial("Anna"));
        Assert.IsTrue(File.Exists(PersistenceSetup.GetPlayerTutorialProgressFilePath("Jarle")));
    }
}
