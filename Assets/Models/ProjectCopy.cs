using System.Text.Json;
using SQLite;

namespace v2.Assets.Models
{
    public class ProjectCopy
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string CopyToPath { get; set; } = string.Empty;
        public int NumberOfPathsToIgnore { get; set; }

        // actual column stored in SQLite as TEXT
        public string PathsToIgnoreJson { get; set; } = "[]";
        
        // what i actually interact with
        [Ignore]
        public List<string>? PathsToIgnore
        {
            get => string.IsNullOrEmpty(PathsToIgnoreJson) 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(PathsToIgnoreJson);
            set => PathsToIgnoreJson = value == null ? "[]" : JsonSerializer.Serialize(value);
        }
    }
}