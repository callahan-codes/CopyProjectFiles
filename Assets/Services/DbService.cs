using SQLite;
using v2.Assets.Models;
using v2.Assets.Interfaces;

namespace v2.Assets.Services
{
    public class DbService : IDbService
    {
        private string _dbPath;

        public DbService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ProjectGrabFiles");
            Directory.CreateDirectory(folder);

            // Path to the .db file
            _dbPath = Path.Combine(folder, "app_data.db");

            // Initialize table
            using var db = new SQLiteConnection(_dbPath);
            db.CreateTable<ProjectCopy>();
        }

        public string GetAppdataPath()
        {
            return _dbPath;
        }
    }
}