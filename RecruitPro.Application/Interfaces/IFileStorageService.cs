using System.IO;
using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces
{
    /// <summary>
    /// Provides secure file storage operations for private object storage.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Uploads a file to object storage and returns the stored object key.
        /// </summary>
        Task<string> UploadFileAsync(Stream stream, string objectName, string contentType);

        /// <summary>
        /// Generates a temporary presigned download URL for a stored object.
        /// </summary>
        Task<string> GetPresignedUrlAsync(string objectName);

        /// <summary>
        /// Deletes a stored object if it exists.
        /// </summary>
        Task DeleteFileAsync(string objectName);
    }
}
