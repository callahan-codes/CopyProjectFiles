namespace v2.Assets.Interfaces
{
    public interface IMenu
    {
        void Graphic();
        void Prompt(Dictionary<ConsoleKey, string> options);
        ConsoleKey GetInput(Dictionary<ConsoleKey, string> options);
    }
}