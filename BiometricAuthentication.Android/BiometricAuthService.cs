using System;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Security.Keystore;
using Android.Support.V4.App;
using Android.Support.V4.Hardware.Fingerprint;
using Android.Util;
using Android.Widget;
using BiometricAuthentication.Droid;
using Java.Security;
using Javax.Crypto;

[assembly: Xamarin.Forms.Dependency(typeof(BiometricAuthService))]
namespace BiometricAuthentication.Droid
{
    public class BiometricAuthService : IBiometricAuthenticateService
    {
        Context context = Android.App.Application.Context;
        private KeyStore keyStore;
        private Cipher cipher;
        private string KEY_NAME = "XamarinLife";
        public static bool IsAutSucess;

        public string GetAuthenticationType()
        {
            return "";
        }

        public Task<bool> AuthenticateUserIDWithTouchID()
        {
            var tcs = new TaskCompletionSource<bool>();//used to wait the mainUI to get the response of the touchId

            KeyguardManager keyguardManager = (KeyguardManager)context.GetSystemService(Context.KeyguardService);
            FingerprintManagerCompat fingerprintManager = FingerprintManagerCompat.From(context);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                if (ActivityCompat.CheckSelfPermission(context, Manifest.Permission.UseBiometric)
                    != (int)Android.Content.PM.Permission.Granted)
                    return tcs.Task;
                if (!fingerprintManager.IsHardwareDetected)
                    Toast.MakeText(context, "FingerPrint authentication permission not enable", ToastLength.Short).Show();
                else
                {
                    if (!fingerprintManager.HasEnrolledFingerprints)
                        Toast.MakeText(context, "Register at least one fingerprint in Settings", ToastLength.Short).Show();
                    else
                    {
                        if (!keyguardManager.IsKeyguardSecure)
                            Toast.MakeText(context, "Lock screen security not enable in Settings", ToastLength.Short).Show();
                        else
                            GenKey();
                        if (CipherInit())
                        {
                            FingerprintManagerCompat.CryptoObject cryptoObject = new FingerprintManagerCompat.CryptoObject(cipher);
                            BiometricHandler handler = new BiometricHandler(context);
                            handler.StartAuthentication(fingerprintManager, cryptoObject);
                        }
                    }
                }
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (ActivityCompat.CheckSelfPermission(context, Manifest.Permission.UseFingerprint)
                    != (int)Android.Content.PM.Permission.Granted)
                    return tcs.Task;
                if (!fingerprintManager.IsHardwareDetected)
                    Toast.MakeText(context, "FingerPrint authentication permission not enable", ToastLength.Short).Show();
                else
                {
                    if (!fingerprintManager.HasEnrolledFingerprints)
                        Toast.MakeText(context, "Register at least one fingerprint in Settings", ToastLength.Short).Show();
                    else
                    {
                        if (!keyguardManager.IsKeyguardSecure)
                            Toast.MakeText(context, "Lock screen security not enable in Settings", ToastLength.Short).Show();
                        else
                            GenKey();
                        if (CipherInit())
                        {
                            FingerprintManagerCompat.CryptoObject cryptoObject = new FingerprintManagerCompat.CryptoObject(cipher);
                            BiometricHandler handler = new BiometricHandler(context);
                            handler.StartAuthentication(fingerprintManager, cryptoObject);
                        }
                    }
                }
            }
            else
            {
                return tcs.Task;
            }
                tcs.SetResult(IsAutSucess);
            return tcs.Task;
        }

        private bool CipherInit()
        {
            try
            {
                cipher = Cipher.GetInstance(KeyProperties.KeyAlgorithmAes
                    + "/"
                    + KeyProperties.BlockModeCbc
                    + "/"
                    + KeyProperties.EncryptionPaddingPkcs7);
                keyStore.Load(null);
                IKey key = (IKey)keyStore.GetKey(KEY_NAME, null);
                cipher.Init(CipherMode.EncryptMode, key);
                return true;
            }
            catch (Exception ex) { return false; }
        }
        private void GenKey()
        {
            keyStore = KeyStore.GetInstance("AndroidKeyStore");
            KeyGenerator keyGenerator = null;
            keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
            keyStore.Load(null);
            keyGenerator.Init(new KeyGenParameterSpec.Builder(KEY_NAME, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeCbc)
                .SetUserAuthenticationRequired(true)
                .SetEncryptionPaddings(KeyProperties
                .EncryptionPaddingPkcs7).Build());
            keyGenerator.GenerateKey();
        }

        public bool fingerprintEnabled()
        {
            Activity activity = MainActivity.FormsContext;
            KeyguardManager keyguardManager = (KeyguardManager)context.GetSystemService(Context.KeyguardService);
            FingerprintManagerCompat fingerprintManager = FingerprintManagerCompat.From(context);

            /*
             *Condition I : Check if the andoid version is device is greater than
             *Pie, since Biometrics is supported by greater devices
             *no fingerprint manager from Android >9.0
             */

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                if (ActivityCompat.CheckSelfPermission(context, Manifest.Permission.UseBiometric) == Android.Content.PM.Permission.Granted)
                {
                    if (fingerprintManager != null && fingerprintManager.IsHardwareDetected)
                    {
                        if (keyguardManager.IsKeyguardSecure)
                        {
                            if (fingerprintManager.HasEnrolledFingerprints)
                            {
                                //user has enrolled one or more fingerprints to authenticate
                                //ShowBiometricPrompt();
                                return true;
                            }
                            else
                            {
                                Log.Error("P Biometric Error", "User not enrolled any fingerprints to authenticate");
                                return false;
                            }
                        }
                        else
                        {
                            Log.Error("P Biometric Error", "Keyguard is not secure");
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("P Biometric Error","Device don't support Biometrics authentication");
                            return false;
                    }
                }
                else
                {
                    Log.Error("P Biometric Error", "User not given permission to access Biometrics");
                    ActivityCompat.RequestPermissions(activity, new string[] { Manifest.Permission.UseBiometric }, 200);
                    return false;
                }
            }

            /*
             *Condition II: check if the android device version is greater than Marshmallow, 
             *since fingerprint authenticatio is only supported from Android 6.0
             */

            else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                //FingerprintManager fingerprintMan = (FingerprintManager)context.GetSystemService(Context.FingerprintService);//.From(context);

                if (ActivityCompat.CheckSelfPermission(context, Manifest.Permission.UseFingerprint) == Android.Content.PM.Permission.Granted)
                {
                    if (fingerprintManager != null && fingerprintManager.IsHardwareDetected)
                    {
                        if (keyguardManager.IsKeyguardSecure)
                        {
                            if (fingerprintManager.HasEnrolledFingerprints)
                            {
                                //user has enrolled one or more fingerprints to authenticate
                                return true;
                            }
                            else
                            {
                                Log.Error("M Biometric Error", "User not enrolled any fingerprints to authenticate");
                                return false;
                            }
                        }
                        else
                        {
                            Log.Error("M Biometric Error", "Keyguard is not secure");
                            return false;
                        }
                    }
                    else
                    {
                        Log.Error("M Biometric Error", "Device don't support Biometrics authentication");
                            return false;
                    }
                }
                else
                {
                    Log.Error("P Biometric Error", "User not given permission to access Biometrics");
                    ActivityCompat.RequestPermissions(activity, new string[] { Manifest.Permission.UseFingerprint }, 200);
                    return false;
                }
            }

            /*
             *Lower version don't support for biometric authentication
             */
            else
            {
                Log.Equals("Biometric Error"," Device don't support Fingerprint");
                return false;
            }

            
        }
    }
}

