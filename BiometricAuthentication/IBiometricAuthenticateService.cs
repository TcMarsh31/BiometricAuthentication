using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace BiometricAuthentication
{
    public interface IBiometricAuthenticateService 
    {
        
            String GetAuthenticationType();
            Task<bool> AuthenticateUserIDWithTouchID();
            bool fingerprintEnabled();
        
    }
}

