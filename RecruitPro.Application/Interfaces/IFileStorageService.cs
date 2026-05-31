using System.IO;
using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(Stream stream, string objectName, string contentType);
        Task DeleteFileAsync(string objectName);
    }
}
