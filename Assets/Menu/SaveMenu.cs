namespace v2.Assets.Menu
{
    public class SaveMenu : MenuBase
    {
        public SaveMenu() {}

        public override void Graphic()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("   '--x' to return to menu");
            Console.WriteLine("   '--b' to go to previous");
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("""
                 --------------------
                |        SAVE        |
            """);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.ResetColor(); 
        }
    }
}