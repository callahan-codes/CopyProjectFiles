namespace v2.Assets.Menu
{
    public class DeleteMenu : MenuBase
    {
        public DeleteMenu() {}

        public override void Graphic()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("   '--x' or '--b' to return to menu");
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("""
                 --------------------
                |       DELETE       |
            """);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.ResetColor(); 
        }
    }
}