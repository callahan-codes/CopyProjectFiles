using v2.Assets.Interfaces;
using v2.Assets.Menu;
using v2.Assets.Services;

// db init
IDbService db = new DbService();
IProjectService pcService = new ProjectService(db);

// main menu init
IMenu menuMain = new MenuBase();
var menuMainOptions = new Dictionary<ConsoleKey, string>
{
    {ConsoleKey.D1, "Create new project copy"},
    {ConsoleKey.D2, "Edit existing project copy details"},
    {ConsoleKey.D3, "Delete existing project copy"},
    {ConsoleKey.D4, "Save project copy to directory"},
    {ConsoleKey.D5, "EXIT"},
};
menuMain.Graphic();
pcService.Display();
menuMain.Prompt(menuMainOptions);

// main app
bool appIsRunning = true;
while (appIsRunning)
{
    // fire case based on input for main menu
    switch (menuMain.GetInput(menuMainOptions))
    {
        // create new
        case ConsoleKey.D1:

            IMenu menuCreate = new CreateMenu();
            menuMain.Graphic();
            pcService.Display();
            menuCreate.Graphic();
            pcService.Create();
            break;

        // edit existing
        case ConsoleKey.D2:
            IMenu menuEdit = new EditMenu();
            menuMain.Graphic();
            pcService.Display();
            menuEdit.Graphic();
            pcService.Edit();
            break;

        // delete existing
        case ConsoleKey.D3:
            IMenu menuDelete = new DeleteMenu();
            menuMain.Graphic();
            pcService.Display();
            menuDelete.Graphic();
            pcService.Delete();
            break;

        // save to new dir
        case ConsoleKey.D4:
            IMenu menuSave = new SaveMenu();
            menuMain.Graphic();
            pcService.Display();
            menuSave.Graphic();
            pcService.Save();
            break;

        // exit
        case ConsoleKey.D5:
            appIsRunning = false;
            continue;
    }   

    // reset terminal
    menuMain.Graphic();
    pcService.Display();
    menuMain.Prompt(menuMainOptions);
}

// destroy
Environment.Exit(0);