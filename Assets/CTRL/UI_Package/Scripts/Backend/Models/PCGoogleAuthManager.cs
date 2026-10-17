using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Backend.Interfaces;
using UnityEngine;

namespace Backend.PlayfabImpl
{
    public class PCGoogleAuthService : IPCGoogleAuthService
    {
        public async Task<string> GetServerAuthCodeAsync(string clientId, int port = 5000)
        {
            string redirectUri = $"http://localhost:{port}/callback/";
            
            // Tạo HTTP Listener
            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add(redirectUri);
                
                try
                {
                    listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    Debug.LogError($"[PCGoogleAuth] Không thể khởi chạy Listener trên cổng {port}: {ex.Message}");
                    return null;
                }

                // Tạo URL đăng nhập Google OAuth2
                string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                                 $"client_id={Uri.EscapeDataString(clientId)}&" +
                                 $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                                 $"response_type=code&" +
                                 $"scope=openid%20email%20profile&" +
                                 $"access_type=offline&" +
                                 $"prompt=consent";

                // Mở trình duyệt mặc định trên PC
                Application.OpenURL(authUrl);

                // Chờ phản hồi từ trình duyệt (Async)
                HttpListenerContext context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                string authCode = request.QueryString["code"];
                string error = request.QueryString["error"];

                // Trả về giao diện thông báo trên Trình Duyệt Web
                string responseHtml = !string.IsNullOrEmpty(authCode)
                    ? "<html><body style='font-family:sans-serif; text-align:center; padding-top:50px;'>" +
                      "<h1 style='color:#4CAF50;'>Đăng nhập thành công!</h1>" +
                      "<p>Bạn có thể đóng cửa sổ này và quay lại Game.</p></body></html>"
                    : $"<html><body style='font-family:sans-serif; text-align:center; padding-top:50px;'>" +
                      $"<h1 style='color:#F44336;'>Đăng nhập thất bại!</h1>" +
                      $"<p>Lỗi: {error}</p></body></html>";

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=utf-8";
                
                using (Stream output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }

                listener.Stop();

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[PCGoogleAuth] Google trả về lỗi: {error}");
                    return null;
                }

                return authCode;
            }
        }
    }
}