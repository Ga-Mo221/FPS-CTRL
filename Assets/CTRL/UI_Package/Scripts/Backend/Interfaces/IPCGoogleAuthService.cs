using System;
using System.Threading.Tasks;

namespace Backend.Interfaces
{
    public interface IPCGoogleAuthService
    {
        /// <summary>
        /// Mở trình duyệt web, lắng nghe localhost và lấy ServerAuthCode từ Google.
        /// </summary>
        Task<string> GetServerAuthCodeAsync(string clientId, int port = 5000);
    }
}