using v2.Assets.Interfaces;

namespace v2.Assets.Menu
{
    public class MenuBase : IMenu
    {
        public MenuBase() {}

        public virtual void Graphic()
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Write("\x1b[2J\x1b[3J\x1b[H"); // clear screen + scrollback, move cursor home
            }
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("""
                ██████ ██████  ███████ 
                ██     ██   ██ ██      
                ██     ██████  █████   
                ██     ██      ██      
                ██████ ██      ██
                 copy   proj   files
            """);
            Console.WriteLine();
            Console.ResetColor(); 
        }

        public virtual void Prompt(Dictionary<ConsoleKey, string> options)
        {
            foreach (var option in options)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                // int intMappedOptionKey = (int)option.Key;s
                int intMappedOptionKey = option.Key - ConsoleKey.D0; 
                Console.Write($"{intMappedOptionKey}] ");
                Console.ResetColor();

                Console.WriteLine($"{option.Value}");
            }

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"\nSelect an option (1-{options.Count})");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(">> ");
            Console.ResetColor();
        }

        public virtual ConsoleKey GetInput(Dictionary<ConsoleKey, string> options)
        {
            ConsoleKeyInfo input = Console.ReadKey();
            ValidateInput(input.Key, options);
            return input.Key;
        }

        private void ValidateInput(ConsoleKey input, Dictionary<ConsoleKey, string> options)
        {
            foreach (var option in options)
            {
                if (option.Key == input)
                {
                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Invalid input -> '{input}'.");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("Press any key to continue... ");
            Console.ResetColor();
            Console.ReadKey();
        }
    }
}