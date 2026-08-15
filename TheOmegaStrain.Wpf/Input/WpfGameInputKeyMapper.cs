using Domain;
using System.Windows.Input;

namespace _3dTesting.Input
{
    public static class WpfGameInputKeyMapper
    {
        public static GameInputKey ToGameInputKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return GameInputKey.A + (key - Key.A);

            if (key >= Key.D0 && key <= Key.D9)
                return GameInputKey.D0 + (key - Key.D0);

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return GameInputKey.NumPad0 + (key - Key.NumPad0);

            return key switch
            {
                Key.Back => GameInputKey.Back,
                Key.Space => GameInputKey.Space,
                Key.OemMinus => GameInputKey.OemMinus,
                Key.Subtract => GameInputKey.Subtract,
                Key.Escape => GameInputKey.Escape,
                Key.Return => GameInputKey.Return,
                Key.Up => GameInputKey.Up,
                Key.Down => GameInputKey.Down,
                Key.Left => GameInputKey.Left,
                Key.Right => GameInputKey.Right,
                _ => GameInputKey.None
            };
        }
    }
}
