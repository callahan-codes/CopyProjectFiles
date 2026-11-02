namespace v2.Assets.Menu
{
    public class EditMenu : MenuBase
    {
        public EditMenu() {}

        public override void Graphic()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("   '--x' to return to menu");
            Console.WriteLine("   '--b' to go to previous");
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("""
                 --------------------
                |        EDIT        |
            """);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.ResetColor(); 
        }
    }
}