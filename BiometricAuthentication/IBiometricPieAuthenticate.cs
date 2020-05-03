using System;
namespace BiometricAuthentication
{
    public interface IBiometricPieAuthenticate
    {
        void RegisterOrAuthenticate();

        bool CheckSDKGreater29();
    }
}
