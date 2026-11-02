using v2.Assets.Models;

namespace v2.Assets.Interfaces
{
    public interface IProjectService
    {
        ProjectCopy? Create();
        ProjectCopy? Edit();
        ProjectCopy? Delete();
        ProjectCopy? Save();
        void Display();
    }
}