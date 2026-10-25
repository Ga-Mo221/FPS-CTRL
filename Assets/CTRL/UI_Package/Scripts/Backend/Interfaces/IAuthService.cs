using System;
using Backend.Models;

namespace Backend.Interfaces
{
    public interface IAuthService
    {
        bool IsLoggedIn {get;}
        string CurrentUserId {get;}
           
        void Register(RegisterRequest request, 
            Action<AuthResult> onSuccess,
            Action<AuthError> onFailure);
        
        void Login(LoginRequest request, 
            Action<AuthResult> onSuccess,
            Action<AuthError> onFailure);
        
        void LoginAsGuest(string deviceId, 
            Action<AuthResult> onSuccess,
            Action<AuthError> onFailure);

        void Logout();
    }
}