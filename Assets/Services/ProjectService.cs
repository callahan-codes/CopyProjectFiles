using SQLite;
using v2.Assets.Interfaces;
using v2.Assets.Menu;
using v2.Assets.Models;

namespace v2.Assets.Services
{
    public class ProjectService : IProjectService
    {
        private IDbService _dbService;
        private IMenu menuMain = new MenuBase();
        private IMenu menuCreate = new CreateMenu();
        private IMenu menuEdit = new EditMenu();
        private IMenu menuDelete = new DeleteMenu();
        private IMenu menuSave = new SaveMenu();
        private enum InputTypes
        {
            Name,
            TargetCopyPath,
            DestinationPath,
            PathsToIgnore,
            EditProjectWithID,
            KeepStructure,
            ClearDestination
        };
        // fixed display order for the edit flow, since EditProjectWithID's enum
        // value (4) is highest but it's always collected first
        private static readonly InputTypes[] EditDisplayOrder =
        [
            InputTypes.EditProjectWithID,
            InputTypes.Name,
            InputTypes.TargetCopyPath,
            InputTypes.DestinationPath,
            InputTypes.PathsToIgnore
        ];


        public ProjectService(IDbService dbService)
        {
            _dbService = dbService;
        }

        // * db methods
        private bool SaveToDatabase(ProjectCopy projectCopy)
        {
            try
            {
                using var db = new SQLiteConnection(_dbService.GetAppdataPath());
                int rowsAffected = db.Insert(projectCopy);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                DisplayMessage($"Failed to add project: {ex.Message}");
                return false;
            }
        }

        private List<ProjectCopy>? GetAllFromDatabase()
        {
            try
            {
                // connect to db, map table to list
                using var db = new SQLiteConnection(_dbService.GetAppdataPath());
                List<ProjectCopy>? projectList = [.. db.Table<ProjectCopy>()];
                return projectList ?? null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        private ProjectCopy? GetSingleFromDatabase(int projectCopyId)
        {
            try
            {
                using var db = new SQLiteConnection(_dbService.GetAppdataPath());
                return db.Find<ProjectCopy>(projectCopyId);
            }
            catch (Exception ex)
            {
                DisplayMessage($"Unexpected error: {ex}");
                return null;
            }
        }
        
        private bool UpdateInDatabase(ProjectCopy projectCopy)
        {
            try
            {
                using var db = new SQLiteConnection(_dbService.GetAppdataPath());
                int rowsAffected = db.Update(projectCopy);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                DisplayMessage($"Failed to update project: {ex.Message}");
                return false;
            }
        }

        private bool DeleteFromDatabase(int projectCopyId)
        {
            try
            {
                using var db = new SQLiteConnection(_dbService.GetAppdataPath());
                int rowsAffected = db.Delete<ProjectCopy>(projectCopyId);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                DisplayMessage($"Failed to delete project: {ex.Message}");
                return false;
            }
        }

        // * non db methods, public
        public void Display()
        {
            List<ProjectCopy>? allProjectCopies = GetAllFromDatabase();

            if (allProjectCopies == null || allProjectCopies.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("|--x ");
                Console.ResetColor();
                Console.WriteLine("No projects found.\n");
                return;
            }

            foreach (var pc in allProjectCopies)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("|--x ");
                Console.ResetColor();
                DisplaySingleAsRow(pc);
            }

            Console.WriteLine();
            return;
        }

        public ProjectCopy? Create()
        {
            // input tracking init
            Dictionary<int, List<string>?> trackedInputs = new();
            int step = 0; // 0=Name, 1=TargetCopyPath, 2=DestinationPath, 3=PathsToIgnore

            while (step < 4)
            {
                if (step < 3)
                {
                    InputTypes currentType = (InputTypes)step;

                    HandleTerminalReset(currentType, trackedInputs);
                    string? rawInput = Console.ReadLine();
                    string? validated = ValidateInput(rawInput, currentType, trackedInputs);

                    // --x, exit to main menu
                    if (validated == null) return null;

                    // --b, previous input
                    if (validated == "--b")
                    {
                        if (step > 0) step--;
                        trackedInputs.Remove(step);
                        continue;
                    }

                    trackedInputs[(int)currentType] = new List<string> { validated };
                    step++;
                }
                // step == 3, PathsToIgnore
                else 
                {
                    string inputForTargetCopyPath = trackedInputs[1]![0];
                    var (ignoredPaths, wentBack) = CollectPathsToIgnore(inputForTargetCopyPath, trackedInputs);

                    if (wentBack)
                    {
                        // back to DestinationPath
                        step--;
                        trackedInputs.Remove(step);
                        continue;
                    }

                    // --x, exit to main menu
                    if (ignoredPaths == null) return null;

                    trackedInputs[(int)InputTypes.PathsToIgnore] = ignoredPaths;
                    step++;
                }
            }

            // unwrap the four validated fields collected by the step loop above
            string inputForName = trackedInputs[0]![0];
            string finalTargetCopyPath = trackedInputs[1]![0];
            string finalDestinationPath = trackedInputs[2]![0];
            List<string> finalIgnoredPaths = trackedInputs[3]!;

            // reset after using directory nav display
            HandleTerminalReset(InputTypes.PathsToIgnore, trackedInputs, false);

            ProjectCopy newProjCopy = new()
            {
                Name = inputForName,
                Path = finalTargetCopyPath,
                CopyToPath = finalDestinationPath,
                NumberOfPathsToIgnore = finalIgnoredPaths.Count,
                PathsToIgnore = finalIgnoredPaths
            };

            DisplaySingle(newProjCopy);
            if (EnsureTrigger(newProjCopy, "creation", trackedInputs))
            {
                if (SaveToDatabase(newProjCopy)) 
                {
                    DisplayMessage($"ProjectCopy {newProjCopy.Name} successfully created", "success");
                    return newProjCopy;
                }
                else
                {
                    DisplayMessage("An unexpected error occured while trying to save the ProjectCopy");
                    return null;
                }
            }
            else
            {
                DisplayMessage("ProjectCopy creation successfully cancelled", "details");
            }

            return null;
        }

        public ProjectCopy? Edit()
        {
            Dictionary<int, List<string>?> trackedInputs = new();
            InputTypes[] steps = [InputTypes.EditProjectWithID, InputTypes.Name, InputTypes.TargetCopyPath, InputTypes.DestinationPath];
            int step = 0;

            while (step < steps.Length)
            {
                InputTypes currentType = steps[step];

                HandleTerminalReset(currentType, trackedInputs, isEditFlow: true);
                string? rawInput = Console.ReadLine();
                string? validated = ValidateInput(rawInput, currentType, trackedInputs, isEditFlow: true);

                // --x, exit to main menu
                if (validated == null) return null;

                // --b, previous input
                if (validated == "--b")
                {
                    if (step > 0)
                    {
                        step--;
                        trackedInputs.Remove((int)steps[step]);
                    }
                    continue;
                }

                trackedInputs[(int)currentType] = new List<string> { validated };
                step++;
            }

            // unwrap the collected fields
            int projectCopyId = int.Parse(trackedInputs[(int)InputTypes.EditProjectWithID]![0]);
            string newName = trackedInputs[(int)InputTypes.Name]![0];
            string newTargetCopyPath = trackedInputs[(int)InputTypes.TargetCopyPath]![0];
            string newDestinationPath = trackedInputs[(int)InputTypes.DestinationPath]![0];

            var (ignoredPaths, wentBack) = CollectPathsToIgnore(newTargetCopyPath, trackedInputs, isEditFlow: true);

            if (wentBack)
            {
                DisplayMessage("Going back from PathsToIgnore isn't wired up yet.", "details");
                return null;
            }

            // --x, exit to main menu
            if (ignoredPaths == null) return null;

            trackedInputs[(int)InputTypes.PathsToIgnore] = ignoredPaths;

            // reset after using directory nav display
            HandleTerminalReset(InputTypes.PathsToIgnore, trackedInputs, false, isEditFlow: true);

            ProjectCopy? projectCopyToEdit = GetSingleFromDatabase(projectCopyId);
            if (projectCopyToEdit == null)
            {
                DisplayMessage($"Could not find ProjectCopy with ID {projectCopyId}", "details");
                return null;
            }
            else
            {
                DisplayMessage("Before edits:", "details", false);
                DisplaySingle(projectCopyToEdit);

                projectCopyToEdit.Name = newName;
                projectCopyToEdit.Path = newTargetCopyPath;
                projectCopyToEdit.CopyToPath = newDestinationPath;
                projectCopyToEdit.NumberOfPathsToIgnore = ignoredPaths.Count;
                projectCopyToEdit.PathsToIgnore = ignoredPaths;

                DisplayMessage("After edits:", "details", false);
                DisplaySingle(projectCopyToEdit);

                if (EnsureTrigger(projectCopyToEdit, "edit", trackedInputs, isEditFlow: true))
                {
                    if (UpdateInDatabase(projectCopyToEdit))
                    {
                        DisplayMessage($"ProjectCopy {projectCopyToEdit.Name} successfully updated", "success");
                        return projectCopyToEdit;
                    }
                    else
                    {
                        DisplayMessage("An unexpected error occured while trying to update the ProjectCopy");
                        return null;
                    }
                }
                else
                {
                    DisplayMessage($"Successfully canceled edit of ProjectCopy '{projectCopyToEdit.Name}'", "details");
                }
            }

            return null;
        }

        public ProjectCopy? Delete()
        {
            Dictionary<int, List<string>?> trackedInputs = new();

            string? validatedInput;
            while (true)
            {
                HandleTerminalReset(InputTypes.EditProjectWithID, trackedInputs, isDeleteFlow: true);
                string? rawInput = Console.ReadLine();
                validatedInput = ValidateInput(rawInput, InputTypes.EditProjectWithID, trackedInputs, isDeleteFlow: true);

                if (validatedInput == null) return null; // main menu
                if (validatedInput == "--b") continue;   // nothing to go back to, redraw

                break;
            }

            int projectCopyId = int.Parse(validatedInput);
            trackedInputs[(int)InputTypes.EditProjectWithID] = new List<string> { validatedInput };

            ProjectCopy? projectCopyToDelete = GetSingleFromDatabase(projectCopyId);
            if (projectCopyToDelete == null)
            {
                DisplayMessage($"Could not find ProjectCopy with ID {projectCopyId}", "details");
                return null;
            }

            HandleTerminalReset(InputTypes.EditProjectWithID, trackedInputs, false, isDeleteFlow: true);
            DisplaySingle(projectCopyToDelete);

            if (EnsureTrigger(projectCopyToDelete, "deletion", trackedInputs, isDeleteFlow: true))
            {
                if (DeleteFromDatabase(projectCopyId))
                {
                    DisplayMessage($"ProjectCopy {projectCopyToDelete.Name} successfully deleted", "success");
                    return projectCopyToDelete;
                }
                else
                {
                    DisplayMessage("An unexpected error occured while trying to delete the ProjectCopy");
                    return null;
                }
            }
            else
            {
                DisplayMessage($"Successfully canceled deletion of ProjectCopy '{projectCopyToDelete.Name}'", "details");
            }

            return null;
        }

        public ProjectCopy? Save()
        {
            Dictionary<int, List<string>?> trackedInputs = new();

            string? validatedInput;
            while (true)
            {
                HandleTerminalReset(InputTypes.EditProjectWithID, trackedInputs, isSaveFlow: true);
                string? rawInput = Console.ReadLine();
                validatedInput = ValidateInput(rawInput, InputTypes.EditProjectWithID, trackedInputs, isSaveFlow: true);

                if (validatedInput == null) return null;  // --x, main menu
                if (validatedInput == "--b") continue;    // nothing to go back to, redraw

                break;
            }

            int projectCopyId = int.Parse(validatedInput);
            trackedInputs[(int)InputTypes.EditProjectWithID] = new List<string> { validatedInput };

            ProjectCopy? projectCopyToSave = GetSingleFromDatabase(projectCopyId);
            if (projectCopyToSave == null)
            {
                DisplayMessage($"Could not find ProjectCopy with ID {projectCopyId}", "details");
                return null;
            }

            HandleTerminalReset(InputTypes.EditProjectWithID, trackedInputs, false, isSaveFlow: true);
            DisplaySingle(projectCopyToSave);

            bool keepStructure = PromptKeepStructure(projectCopyToSave, trackedInputs, isSaveFlow: true);
            trackedInputs[(int)InputTypes.KeepStructure] = new List<string> { keepStructure ? "Yes" : "No" };

            bool clearDestination = PromptClearDestination(projectCopyToSave, trackedInputs, isSaveFlow: true);
            trackedInputs[(int)InputTypes.ClearDestination] = new List<string> { clearDestination ? "Yes" : "No" };

            List<string> pathsToIgnore = projectCopyToSave.PathsToIgnore ?? new List<string>();
            var (filesToCopy, ignoredCount) = CollectFilesToCopy(projectCopyToSave.Path, pathsToIgnore);

            if (filesToCopy.Count == 0)
            {
                DisplayMessage("No files found to copy (after applying ignored paths).", "details");
                return null;
            }

            if (!EnsureTrigger(projectCopyToSave, "save", trackedInputs, isSaveFlow: true))
            {
                DisplayMessage($"Successfully canceled save of ProjectCopy '{projectCopyToSave.Name}'", "details");
                return null;
            }

            Directory.CreateDirectory(projectCopyToSave.CopyToPath);
            if (clearDestination)
            {
                ClearDirectory(projectCopyToSave.CopyToPath);
            }
            var (copied, failed) = PerformCopy(filesToCopy, projectCopyToSave.CopyToPath, keepStructure);

            DisplayMessage($"ProjectCopy '{projectCopyToSave.Name}' saved: {copied} copied, {ignoredCount} ignored, {failed} failed.", "success");

            return projectCopyToSave;
        }

        // * helpers

        // checks if input is valid, will return false only when user wants back to main menu,
        // otherwise it loops until true.
        private string? ValidateInput(string? input, InputTypes inputType, Dictionary<int, List<string>?> previousInputs, bool isEditFlow = false, bool isDeleteFlow = false, bool isSaveFlow = false)
        {
            bool isCurrentlyValidating = true;
            while (isCurrentlyValidating)
            {
                if (input == "--x") return null;
                if (input == "--b") return "--b";

                if (input == null || string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
                {
                    DisplayMessage($"Invalid input -> '{input}'");
                    HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                    input = Console.ReadLine();
                    continue;
                }
                
                if (inputType == InputTypes.TargetCopyPath || inputType == InputTypes.DestinationPath)
                {
                    if (!Path.Exists(input))
                    {
                        DisplayMessage($"Path doesn't exist -> '{input}'");
                        HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                        input = Console.ReadLine();
                        continue;
                    }

                    if (inputType == InputTypes.DestinationPath)
                    {
                        bool isMatch = previousInputs.GetValueOrDefault((int)InputTypes.TargetCopyPath)?.Contains(input) ?? false;
                        if (isMatch)
                        {
                            DisplayMessage($"DestinationPath is same as TargetCopyPath '{input}'", "error", false);
                            DisplayMessage("If DestinationPath and TargetCopyPath were the same, your actual project files would get bloated with inserted copies", "details");
                            HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                            input = Console.ReadLine();
                            continue;
                        }
                    }
                }

                if (inputType == InputTypes.EditProjectWithID)
                {
                    List<ProjectCopy>? projectCopies = GetAllFromDatabase();
                    if (projectCopies == null || projectCopies.Count == 0)
                    {
                        DisplayMessage("No projects found.", "details");
                        HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                        input = Console.ReadLine();
                        continue;
                    }
                    else
                    {
                        if (int.TryParse(input, out int result))
                        {
                            bool found = false;
                            foreach (var pc in projectCopies)
                            {
                                if (pc.Id == result)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                DisplayMessage($"No project with ID {result} was found.");
                                HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                                input = Console.ReadLine();
                                continue;
                            }
                        }
                        else
                        {
                            DisplayMessage("Invalid input (could not convert to integer).");
                            HandleTerminalReset(inputType, previousInputs, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                            input = Console.ReadLine();
                            continue;
                        }
                    }
                }

                isCurrentlyValidating = false;
            }

            return input;
        }

        // displays a message, defaults as an error message 'DisplayMessage(error)' but is 
        // also used as a normal message handler
        private void DisplayMessage(string message, string type = "error", bool allowClickPrompt = true)
        {
            if (type == "details") Console.ForegroundColor = ConsoleColor.DarkYellow;
            else if (type == "success") Console.ForegroundColor = ConsoleColor.DarkGreen;
            else if (type == "tracked") Console.ForegroundColor = ConsoleColor.DarkMagenta;
            else Console.ForegroundColor = ConsoleColor.Red;
            
            Console.WriteLine($"[{type.ToUpper()}] {message}.");

            if (allowClickPrompt)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("Press any key to continue... ");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Console.ResetColor();
        }

        // helper to reset terminal with appropriate data alrady inserted by user
        private void HandleTerminalReset(InputTypes inputType, Dictionary<int, List<string>?> previousInputs, bool allowInputTypeTrail = true, bool isEditFlow = false, bool isDeleteFlow = false, bool isSaveFlow = false)
        {
            menuMain.Graphic();
            Display();

            if (isDeleteFlow)
            {
                menuDelete.Graphic();
            }
            else if (isSaveFlow)
            {
                menuSave.Graphic();
            }
            else if (isEditFlow)
            {
                menuEdit.Graphic();
            }
            else
            {
                menuCreate.Graphic();
            }

            IEnumerable<int> orderedKeys = isEditFlow
                ? EditDisplayOrder.Select(t => (int)t).Where(previousInputs.ContainsKey)
                : previousInputs.Keys.OrderBy(k => k);

            foreach (var key in orderedKeys)
            {
                InputTypes currentType = (InputTypes)key;

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"[{currentType}] ");
                Console.ResetColor();
                Console.WriteLine($"{string.Join(", ", previousInputs[key] ?? new List<string>())}");
            }

            if (allowInputTypeTrail)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"[{inputType}] ");
            }

            Console.ResetColor();
        }

        // helper to display a single formatted proj copy
        private void DisplaySingle(ProjectCopy projectCopy)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(" --------");
            Console.Write($"| ");
            Console.ResetColor();
            Console.WriteLine($"{projectCopy.Id}");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"| ");
            Console.ResetColor();
            Console.WriteLine($"\t[Name]      {projectCopy.Name}");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"| ");
            Console.ResetColor();
            Console.WriteLine($"\t[Copy From] {projectCopy.Path}");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"| ");
            Console.ResetColor();
            Console.WriteLine($"\t[Copy To]   {projectCopy.CopyToPath}");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"| ");
            Console.ResetColor();
            Console.WriteLine($"\t[Ignored]   {projectCopy.NumberOfPathsToIgnore}");

            if (projectCopy.PathsToIgnore != null && projectCopy.PathsToIgnore.Count > 0)
            {
                for (int i = 0; i < projectCopy.PathsToIgnore.Count; i++)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write($"|\t  ∟");
                    Console.ResetColor();
                    Console.WriteLine($"{projectCopy.PathsToIgnore[i]}");                   
                }
            }

            Console.WriteLine();

        }

        // helper to display a single formatted proj copy on a single row
        private void DisplaySingleAsRow(ProjectCopy projectCopy)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("[ ");
            Console.ResetColor();

            Console.Write($"ID: {projectCopy.Id}");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  --  ");
            Console.ResetColor();

            Console.Write($"Name: {projectCopy.Name}");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  --  ");
            Console.ResetColor();

            Console.Write($"Project Path: {projectCopy.Path}");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  --  ");
            Console.ResetColor();

            Console.Write($"Copy to Path: {projectCopy.CopyToPath}");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  --  ");
            Console.ResetColor();

            Console.Write($"Number of Ignored Files: {projectCopy.NumberOfPathsToIgnore}");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(" ]");
            Console.ResetColor();
        }

        // collect paths to ignore for a single project with folder expansion support.
        // returns (null, false) if the user cancelled entirely (--x),
        // returns (null, true) if the user wants to go back to the previous step (--b),
        // returns (pathsToIgnore, false) on normal completion (--c).
        private (List<string>? paths, bool wentBack) CollectPathsToIgnore(string path, Dictionary<int, List<string>?> previousInputs, bool isEditFlow = false)
        {
            List<string> pathsToIgnore = new();
            HashSet<string> expandedFolders = new(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                List<string> validRelativePaths = DisplayDirectoryWithExpansions(path, expandedFolders, pathsToIgnore, previousInputs, isEditFlow);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\nEnter names to exclude (separated by spaces). Use folder/name if a name is ambiguous, e.g. folder1/bin");
                Console.WriteLine("Type 'open <folder path>' to view inside a folder (use / for nested, e.g. open a/b)");
                Console.WriteLine("Type 'close <folder path>' to collapse a folder");
                Console.WriteLine("Type 'untrack <name>' to re-include a previously ignored folder/file");
                Console.WriteLine("Enter '--c' to continue, '--b' to go back, '--x' to cancel\n");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n>> ");
                Console.ResetColor();

                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    DisplayMessage("Invalid input, try again");
                    continue;
                }

                string trimmedInput = userInput.Trim();

                if (trimmedInput.Equals("--x", StringComparison.OrdinalIgnoreCase))
                {
                    // user wants to cancel entirely
                    return (null, false);
                }
                if (trimmedInput.Equals("--b", StringComparison.OrdinalIgnoreCase))
                {
                    // user wants to go back to the previous step
                    return (null, true);
                }
                if (trimmedInput.Equals("--c", StringComparison.OrdinalIgnoreCase))
                {
                    // continue to creation
                    return (pathsToIgnore, false);
                }

                if (trimmedInput.StartsWith("open ", StringComparison.OrdinalIgnoreCase))
                {
                    string folderArg = trimmedInput.Substring(5).Trim();
                    HandleOpenFolder(path, folderArg, expandedFolders);
                    continue;
                }

                if (trimmedInput.StartsWith("close ", StringComparison.OrdinalIgnoreCase))
                {
                    string folderArg = trimmedInput.Substring(6).Trim();
                    HandleCloseFolder(folderArg, expandedFolders);
                    continue;
                }

                if (trimmedInput.StartsWith("untrack ", StringComparison.OrdinalIgnoreCase))
                {
                    string untrackArg = trimmedInput.Substring(8).Trim();
                    HandleUntrack(untrackArg, pathsToIgnore);
                    continue;
                }

                // otherwise, process space-separated tokens as ignore targets
                string[] inputs = userInput.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var input in inputs)
                {
                    string token = input.Trim().Replace('\\', '/');

                    string? resolvedRelativePath = ResolveIgnoreToken(token, validRelativePaths);

                    if (resolvedRelativePath != null)
                    {
                        if (!pathsToIgnore.Contains(resolvedRelativePath))
                        {
                            pathsToIgnore.Add(resolvedRelativePath);
                        }
                    }
                }
            }
        }

        // removes a previously-tracked (ignored) relative path, re-including it.
        // resolves the same way as ignore-tokens: exact relative path, or unambiguous short name.
        private void HandleUntrack(string token, List<string> pathsToIgnore)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                DisplayMessage("Please specify a folder or file to untrack.");
                return;
            }

            string normalizedToken = token.Replace('\\', '/');

            // exact relative path match against what's currently tracked
            var exactMatch = pathsToIgnore.FirstOrDefault(v => v.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                pathsToIgnore.Remove(exactMatch);
                return;
            }

            // otherwise treat token as a short name and match against the last segment of tracked paths
            var shortNameMatches = pathsToIgnore
                .Where(v =>
                {
                    string lastSegment = v.Contains('/') ? v[(v.LastIndexOf('/') + 1)..] : v;
                    return lastSegment.Equals(normalizedToken, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (shortNameMatches.Count == 1)
            {
                pathsToIgnore.Remove(shortNameMatches[0]);
                return;
            }

            if (shortNameMatches.Count > 1)
            {
                DisplayMessage($"'{token}' is ambiguous, matches: {string.Join(", ", shortNameMatches)}. Specify the full path, e.g. {shortNameMatches[0]}");
                return;
            }

            DisplayMessage($"'{token}' is not currently tracked");
        }

        // resolves a user-typed token (either a full relative path like "folder1/bin",
        // or a bare short name like "bin") to a single relative path from validRelativePaths.
        // prints an error and returns null if not found or ambiguous.
        private string? ResolveIgnoreToken(string token, List<string> validRelativePaths)
        {
            // exact relative path match, e.g. "folder1/bin"
            var exactMatch = validRelativePaths.FirstOrDefault(v => v.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            // otherwise treat token as a short name and match against the last segment of every relative path
            var shortNameMatches = validRelativePaths
                .Where(v =>
                {
                    string lastSegment = v.Contains('/') ? v[(v.LastIndexOf('/') + 1)..] : v;
                    return lastSegment.Equals(token, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (shortNameMatches.Count == 1)
            {
                return shortNameMatches[0];
            }

            if (shortNameMatches.Count > 1)
            {
                DisplayMessage($"'{token}' is ambiguous, matches: {string.Join(", ", shortNameMatches)}. Specify the full path, e.g. {shortNameMatches[0]}");
                return null;
            }

            DisplayMessage($"Folder or file with name {token} could not be found");
            return null;
        }

        // opens a folder given a relative path like "folder" or "folder/subfolder".
        // expands every ancestor segment along the way, and preserves spaces in names.
        private void HandleOpenFolder(string rootPath, string relativeFolderPath, HashSet<string> expandedFolders)
        {
            if (string.IsNullOrWhiteSpace(relativeFolderPath))
            {
                DisplayMessage("Please specify a folder to open.");
                return;
            }

            string normalized = relativeFolderPath.Replace('\\', '/').Trim('/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string currentRelative = "";
            string currentFullPath = rootPath;

            foreach (var segment in segments)
            {
                currentFullPath = Path.Combine(currentFullPath, segment);
                currentRelative = currentRelative.Length == 0 ? segment : $"{currentRelative}/{segment}";

                if (!Directory.Exists(currentFullPath))
                {
                    DisplayMessage($"Folder '{currentRelative}' not found");
                    return;
                }

                expandedFolders.Add(currentRelative);
            }
        }

        // closes a folder (and any of its already-open descendants) given a relative path.
        private void HandleCloseFolder(string relativeFolderPath, HashSet<string> expandedFolders)
        {
            if (string.IsNullOrWhiteSpace(relativeFolderPath))
            {
                DisplayMessage("Please specify a folder to close.");
                return;
            }

            string normalized = relativeFolderPath.Replace('\\', '/').Trim('/');

            bool removed = expandedFolders.Remove(normalized);
            expandedFolders.RemoveWhere(f => f.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));

            if (!removed)
            {
                DisplayMessage($"Folder '{normalized}' is not currently open");
            }
        }

        // displays root contents, with expanded folders listed indented underneath recursively.
        // returns every visible item's RELATIVE PATH (e.g. "bin", "folder1/bin"), not just short names.
        private List<string> DisplayDirectoryWithExpansions(string projectPath, HashSet<string> expandedFolders, List<string> pathsToIgnore, Dictionary<int, List<string>?> previousInputs, bool isEditFlow = false)
        {
            List<string> allValidRelativePaths = new();

            HandleTerminalReset(InputTypes.PathsToIgnore, previousInputs, isEditFlow: isEditFlow);

            try
            {
                if (Directory.Exists(projectPath))
                {
                    Console.WriteLine();
                    string[] directories = Directory.GetDirectories(projectPath);
                    if (directories.Length == 0) Console.WriteLine("  (none)");

                    foreach (var dir in directories)
                    {
                        string folderName = Path.GetFileName(dir);
                        allValidRelativePaths.Add(folderName);

                        Console.Write($"\t[Folder] {folderName}/");
                        WriteIgnoreStatus(pathsToIgnore.Contains(folderName));
                        Console.WriteLine();

                        if (expandedFolders.Contains(folderName))
                        {
                            PrintNestedDirectoryContents(dir, folderName, "    ", expandedFolders, pathsToIgnore, allValidRelativePaths);
                        }
                    }

                    string[] files = Directory.GetFiles(projectPath);
                    if (files.Length == 0) Console.WriteLine("  (none)");

                    foreach (var file in files)
                    {
                        FileInfo fi = new FileInfo(file);
                        allValidRelativePaths.Add(fi.Name);

                        Console.Write($"\t[File]   {fi.Name} ({fi.Length} bytes)");
                        WriteIgnoreStatus(pathsToIgnore.Contains(fi.Name));
                        Console.WriteLine();
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access denied to the directory.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            if (pathsToIgnore.Count != 0)
            {
                DisplayMessage($"{string.Join(", ", pathsToIgnore)}", "tracked", false);
            }
            else
            {
                Console.Write("\n[TRACKED]: none");
            }

            return allValidRelativePaths;
        }

        // helper to recursively print nested folder contents, tracking relative path for both
        // expansion lookups and ignore-status lookups (so folder1/bin and folder2/bin are distinct).
        private void PrintNestedDirectoryContents(string folderPath, string relativePath, string indent, HashSet<string> expandedFolders, List<string> pathsToIgnore, List<string> allValidRelativePaths)
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(folderPath))
                {
                    string subFolderName = Path.GetFileName(subDir);
                    string subRelativePath = $"{relativePath}/{subFolderName}";
                    allValidRelativePaths.Add(subRelativePath);

                    Console.Write($"{indent}[Folder] {subFolderName}/");
                    WriteIgnoreStatus(pathsToIgnore.Contains(subRelativePath));
                    Console.WriteLine();

                    if (expandedFolders.Contains(subRelativePath))
                    {
                        PrintNestedDirectoryContents(subDir, subRelativePath, indent + "    ", expandedFolders, pathsToIgnore, allValidRelativePaths);
                    }
                }

                foreach (var file in Directory.GetFiles(folderPath))
                {
                    FileInfo fi = new FileInfo(file);
                    string fileRelativePath = $"{relativePath}/{fi.Name}";
                    allValidRelativePaths.Add(fileRelativePath);

                    Console.Write($"{indent}[File]   {fi.Name} ({fi.Length} bytes)");
                    WriteIgnoreStatus(pathsToIgnore.Contains(fileRelativePath));
                    Console.WriteLine();
                }
            }
            catch
            {
                Console.WriteLine($"{indent}(Unable to read contents)");
            }
        }

        // recursively collects (fullPath, relativePath) for every non-ignored file under rootPath,
        // and counts how many files were excluded because they fell under an ignored path
        private (List<(string fullPath, string relativePath)> filesToCopy, int ignoredCount) CollectFilesToCopy(string rootPath, List<string> pathsToIgnore)
        {
            List<(string fullPath, string relativePath)> results = new();
            int ignoredCount = 0;
            CollectFilesRecursive(rootPath, "", pathsToIgnore, results, ref ignoredCount);
            return (results, ignoredCount);
        }

        private void CollectFilesRecursive(string currentPath, string relativePrefix, List<string> pathsToIgnore, List<(string fullPath, string relativePath)> results, ref int ignoredCount)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(currentPath))
                {
                    string folderName = Path.GetFileName(dir);
                    string relativeFolder = relativePrefix.Length == 0 ? folderName : $"{relativePrefix}/{folderName}";

                    if (IsPathIgnored(relativeFolder, pathsToIgnore))
                    {
                        // count every file inside the ignored folder, since none of them get walked individually
                        try
                        {
                            ignoredCount += Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
                        }
                        catch (UnauthorizedAccessException) { }
                        continue;
                    }

                    CollectFilesRecursive(dir, relativeFolder, pathsToIgnore, results, ref ignoredCount);
                }

                foreach (var file in Directory.GetFiles(currentPath))
                {
                    string fileName = Path.GetFileName(file);
                    string relativeFile = relativePrefix.Length == 0 ? fileName : $"{relativePrefix}/{fileName}";

                    if (IsPathIgnored(relativeFile, pathsToIgnore))
                    {
                        ignoredCount++;
                        continue;
                    }

                    results.Add((file, relativeFile));
                }
            }
            catch (UnauthorizedAccessException)
            {
                DisplayMessage($"Access denied to '{currentPath}', skipping", "details", false);
            }
        }

        // copies each file to destinationRoot, either preserving its relative subpath or
        // flattening into destinationRoot's root (renaming on name collisions when flattened)
        private (int copied, int skipped) PerformCopy(List<(string fullPath, string relativePath)> filesToCopy, string destinationRoot, bool keepStructure)
        {
            int copied = 0;
            int skipped = 0;
            HashSet<string> usedFlatNames = new(StringComparer.OrdinalIgnoreCase);

            foreach (var (fullPath, relativePath) in filesToCopy)
            {
                try
                {
                    string destPath;

                    if (keepStructure)
                    {
                        destPath = Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    }
                    else
                    {
                        string fileName = Path.GetFileName(fullPath);
                        string candidateName = fileName;
                        int counter = 1;

                        // avoid overwriting when the same filename appears in multiple source folders
                        while (usedFlatNames.Contains(candidateName))
                        {
                            candidateName = $"{Path.GetFileNameWithoutExtension(fileName)} ({counter}){Path.GetExtension(fileName)}";
                            counter++;
                        }

                        usedFlatNames.Add(candidateName);
                        destPath = Path.Combine(destinationRoot, candidateName);
                    }

                    File.Copy(fullPath, destPath, overwrite: true);
                    copied++;
                }
                catch (Exception ex)
                {
                    DisplayMessage($"Failed to copy '{relativePath}': {ex.Message}", "error", false);
                    skipped++;
                }
            }

            return (copied, skipped);
        }

        // deletes all files and subfolders inside dirPath, leaving dirPath itself intact.
        // safe to call even if dirPath doesn't exist yet.
        private void ClearDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            foreach (var file in Directory.GetFiles(dirPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    DisplayMessage($"Failed to delete '{file}': {ex.Message}", "error", false);
                }
            }

            foreach (var subDir in Directory.GetDirectories(dirPath))
            {
                try
                {
                    Directory.Delete(subDir, recursive: true);
                }
                catch (Exception ex)
                {
                    DisplayMessage($"Failed to delete '{subDir}': {ex.Message}", "error", false);
                }
            }
        }

        // writes " [IGNORED]" in dark magenta if isIgnored is true; writes nothing otherwise.
        // leaves Console color reset afterward so subsequent writes aren't affected.
        private void WriteIgnoreStatus(bool isIgnored)
        {
            if (!isIgnored) return;

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.Write(" [IGNORED]");
            Console.ResetColor();
        }
    
        // true if relativePath is itself an ignored entry, or nested inside one
        private bool IsPathIgnored(string relativePath, List<string> pathsToIgnore)
        {
            foreach (var ignored in pathsToIgnore)
            {
                if (relativePath.Equals(ignored, StringComparison.OrdinalIgnoreCase)) return true;
                if (relativePath.StartsWith(ignored + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // helper that prompts the user y/n for any perma change to a projectcopy
        // takes confirmtype param for displaying message 'confirm "create/edit/delete/etc" of projectcopy ID'
        private bool EnsureTrigger(ProjectCopy projectCopy, string confirmType, Dictionary<int, List<string>?> previousInputs, bool isEditFlow = false, bool isDeleteFlow = false, bool isSaveFlow = false)
        {
            while (true)
            {
                DisplayMessage($"Confirm {confirmType} of ProjectCopy named '{projectCopy.Name}' [ID={projectCopy.Id}]", "details", false);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Press 'y' (yes) or 'n' (no) to confirm.");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n>> ");
                Console.ResetColor();
                ConsoleKeyInfo input = Console.ReadKey();
                Console.WriteLine();

                if (input.Key != ConsoleKey.Y && input.Key != ConsoleKey.N)
                {
                    DisplayMessage($"Invalid input -> '{input.Key}'");
                    HandleTerminalReset(InputTypes.PathsToIgnore, previousInputs, false, isEditFlow: isEditFlow, isDeleteFlow: isDeleteFlow, isSaveFlow: isSaveFlow);
                    DisplaySingle(projectCopy);
                    continue;
                } 
                else
                {
                    if (input.Key == ConsoleKey.Y) return true;
                    else return false;
                } 
            }
        }
    
        // helper that prompts y/n to keep the source directory structure (y) or flatten everything into one folder (n)
        private bool PromptKeepStructure(ProjectCopy projectCopy, Dictionary<int, List<string>?> previousInputs, bool isSaveFlow = false)
        {
            while (true)
            {
                DisplayMessage("Keep the original directory structure in the copy?", "details", false);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Press 'y' to keep structure, 'n' to flatten all files into one folder.");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n>> ");
                Console.ResetColor();
                ConsoleKeyInfo input = Console.ReadKey();
                Console.WriteLine();

                if (input.Key == ConsoleKey.Y) return true;
                if (input.Key == ConsoleKey.N) return false;

                DisplayMessage($"Invalid input -> '{input.Key}'");
                HandleTerminalReset(InputTypes.EditProjectWithID, previousInputs, false, isSaveFlow: isSaveFlow);
                DisplaySingle(projectCopy);
            }
        }
    
        // helper that prompts y/n to clear CopyToPath's existing contents before writing the new copy (y) or leave them in place (n)
        private bool PromptClearDestination(ProjectCopy projectCopy, Dictionary<int, List<string>?> previousInputs, bool isSaveFlow = false)
        {
            while (true)
            {
                DisplayMessage($"Clear existing contents of '{projectCopy.CopyToPath}' before copying?", "details", false);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Press 'y' to clear the destination first, 'n' to copy into it as-is.");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n>> ");
                Console.ResetColor();
                ConsoleKeyInfo input = Console.ReadKey();
                Console.WriteLine();

                if (input.Key == ConsoleKey.Y) return true;
                if (input.Key == ConsoleKey.N) return false;

                DisplayMessage($"Invalid input -> '{input.Key}'");
                HandleTerminalReset(InputTypes.EditProjectWithID, previousInputs, false, isSaveFlow: isSaveFlow);
                DisplaySingle(projectCopy);
            }
        }
    }
}