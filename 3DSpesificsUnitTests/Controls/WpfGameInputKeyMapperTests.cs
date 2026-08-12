using _3dTesting.Input;
using Domain;
using System.Windows.Input;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class WpfGameInputKeyMapperTests
{
    [TestMethod]
    public void ToGameInputKey_MapsLettersAndDigits()
    {
        Assert.AreEqual(GameInputKey.A, WpfGameInputKeyMapper.ToGameInputKey(Key.A));
        Assert.AreEqual(GameInputKey.Z, WpfGameInputKeyMapper.ToGameInputKey(Key.Z));
        Assert.AreEqual(GameInputKey.D0, WpfGameInputKeyMapper.ToGameInputKey(Key.D0));
        Assert.AreEqual(GameInputKey.D9, WpfGameInputKeyMapper.ToGameInputKey(Key.D9));
        Assert.AreEqual(GameInputKey.NumPad0, WpfGameInputKeyMapper.ToGameInputKey(Key.NumPad0));
        Assert.AreEqual(GameInputKey.NumPad9, WpfGameInputKeyMapper.ToGameInputKey(Key.NumPad9));
    }

    [TestMethod]
    public void ToGameInputKey_MapsNavigationAndEditingKeys()
    {
        Assert.AreEqual(GameInputKey.Escape, WpfGameInputKeyMapper.ToGameInputKey(Key.Escape));
        Assert.AreEqual(GameInputKey.Return, WpfGameInputKeyMapper.ToGameInputKey(Key.Return));
        Assert.AreEqual(GameInputKey.Space, WpfGameInputKeyMapper.ToGameInputKey(Key.Space));
        Assert.AreEqual(GameInputKey.Back, WpfGameInputKeyMapper.ToGameInputKey(Key.Back));
        Assert.AreEqual(GameInputKey.Left, WpfGameInputKeyMapper.ToGameInputKey(Key.Left));
        Assert.AreEqual(GameInputKey.Right, WpfGameInputKeyMapper.ToGameInputKey(Key.Right));
        Assert.AreEqual(GameInputKey.Up, WpfGameInputKeyMapper.ToGameInputKey(Key.Up));
        Assert.AreEqual(GameInputKey.Down, WpfGameInputKeyMapper.ToGameInputKey(Key.Down));
    }

    [TestMethod]
    public void ToGameInputKey_ReturnsNoneForUnhandledKeys()
    {
        Assert.AreEqual(GameInputKey.None, WpfGameInputKeyMapper.ToGameInputKey(Key.LeftShift));
    }
}
