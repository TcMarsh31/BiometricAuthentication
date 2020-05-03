using System;
using System.Text;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Runtime;
using Android.Security.Keystore;
using Android.Util;
using BiometricAuthentication.Droid;
using Java.Lang;
using Java.Security;
using Java.Security.Spec;
using Java.Util.Concurrent;
using Xamarin.Forms;

[assembly: Xamarin.Forms.Dependency(typeof(TouchIdAuthService))]
namespace BiometricAuthentication.Droid
{
    public class TouchIdAuthService : IBiometricPieAuthenticate
    {
        private const string KEY_STORE_NAME = "AndroidKeyStore";
        private const string KEY_NAME = "BiometricKey";
        private const string REPLAY_ID = "12345";//Set random value?
        private const string SIGNATURE_ALGORITHM = "SHA256withECDSA";

        private BiometricPrompt biometricPrompt;
        private string signatureMessage;

        public TouchIdAuthService()
        {
        }

        public void RegisterOrAuthenticate()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.P)
            {
                //BiometricPrompt.PromptInfo not yet supported by XamarinForms.
                /*var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Login")
                .SetNegativeButtonText("Cancel")
                .Build();

                var activity = _activityResolver();
                var executor = Executors.NewSingleThreadExecutor();
                var handler = new BiometricsAuthenticationHandler();

                using var dialog = new BiometricPrompt((FragmentActivity)activity, executor, handler);
                await using (cancellationToken.Register(() => dialog.CancelAuthentication()))
                {
                    dialog.Authenticate(promptInfo);
                    return await handler.Task;
                }*/
            }
            //throw new NotSupportedException("Fingerprint not supported below Android 9 Pie.");
            else
            {
                var alreadyRegistered = false;
                if (!alreadyRegistered)
                    Register();
                else
                    Authenticate();
            }
        }

        private void Register()
        {
            if (IsSupported)
            {
                // Generate key pair and init signature
                Java.Security.Signature signature;
                try
                {
                    KeyPair keyPair = GenerateKeyPair(KEY_NAME, true);
                    // Send public key part of key pair to the server, to be used for authentication
                    signatureMessage = "{0}:{1}:{2}"+
                        string.Format(Base64.EncodeToString(keyPair.Public.GetEncoded(), Base64Flags.UrlSafe), KEY_NAME, REPLAY_ID);
                    signature = InitSignature(KEY_NAME);
                }
                catch (Java.Lang.Exception e)
                {
                    throw new RuntimeException(e);
                }

                if (signature != null)
                    ShowBiometricPrompt(signature);
            }
        }

        private void Authenticate()
        {
            if (IsSupported)
            {
                // Init signature
                Java.Security.Signature signature;
                try
                {
                    signatureMessage = "{0}:{1}"+string.Format(KEY_NAME, REPLAY_ID);
                    signature = InitSignature(KEY_NAME);
                }
                catch (Java.Lang.Exception e)
                {
                    throw new RuntimeException(e);
                }

                if (signature != null)
                    ShowBiometricPrompt(signature);
            }
        }

        private void ShowBiometricPrompt(Java.Security.Signature signature)
        {
            // Create biometric prompt
            var activity = MainActivity.FormsContext;
            var negativeButtonListener = new DialogInterfaceOnClickListener(() => {
                //  Do something here. 
            });


            biometricPrompt = new BiometricPrompt.Builder(activity)
                .SetDescription("Never Been Easier")
                .SetTitle("Biometric Prompt Authentication")
                .SetSubtitle("Please allow Xamarin Life to authenticate")
                .SetNegativeButton("Cancel", activity.MainExecutor, negativeButtonListener)
                .Build();

            // Show biometric prompt
            var cancellationSignal = new CancellationSignal();
            var authenticationCallback = GetAuthenticationCallback();
            biometricPrompt.Authenticate(new BiometricPrompt.CryptoObject(signature), cancellationSignal, activity.MainExecutor, authenticationCallback);
        }

        private BiometricPrompt.AuthenticationCallback GetAuthenticationCallback()
        {
            // Callback for biometric authentication result
            var callback = new BiometricAuthenticationCallback
            {
                Success = (BiometricPrompt.AuthenticationResult result) => {
                    var signature = result.CryptoObject.Signature;
                    try
                    {
                        signature.Update(Encoding.ASCII.GetBytes(signatureMessage));
                        var signatureString = Base64.EncodeToString(signature.Sign(), Base64Flags.UrlSafe);
                        // Normally, ToBeSignedMessage and Signature are sent to the server and then verified
                        //  Toast.MakeText (getApplicationContext (), signatureMessage + ":" + signatureString, Toast.LENGTH_SHORT).show ();
                        MessagingCenter.Send<object>("BiometricPrompt","Success");
                    }
                    catch (SignatureException)
                    {
                        throw new RuntimeException();
                    }
                },
                Failed = () => {
                    MessagingCenter.Send<object>("BiometricPrompt", "Fail");
                    //  Show error.
                },
                Help = (BiometricAcquiredStatus helpCode, ICharSequence helpString) => {
                   //below BiometricAcquiredStatus falls here
                    //BiometricAcquiredStatus.ImagerDirty;
                    //BiometricAcquiredStatus.Insufficient;
                    //BiometricAcquiredStatus.Partial;
                    //BiometricAcquiredStatus.TooFast;
                    //BiometricAcquiredStatus.TooSlow;// 
                }
            };
            return callback;
        }

        private KeyPair GenerateKeyPair(string keyName, bool invalidatedByBiometricEnrollment)
        {
            var keyPairGenerator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, KEY_STORE_NAME);
            var builder = new KeyGenParameterSpec.Builder(keyName, KeyStorePurpose.Sign)
                .SetAlgorithmParameterSpec(new ECGenParameterSpec("secp256r1"))
                .SetDigests(KeyProperties.DigestSha256, KeyProperties.DigestSha384, KeyProperties.DigestSha512)
                // Require the user to authenticate with a biometric to authorize every use of the key
                .SetUserAuthenticationRequired(true)
                // Generated keys will be invalidated if the biometric templates are added more to user device
                .SetInvalidatedByBiometricEnrollment(invalidatedByBiometricEnrollment);

            keyPairGenerator.Initialize(builder.Build());

            return keyPairGenerator.GenerateKeyPair();
        }

        private KeyPair GetKeyPair(string keyName)
        {
            var keyStore = KeyStore.GetInstance(KEY_STORE_NAME);
            keyStore.Load(null);
            if (keyStore.ContainsAlias(keyName))
            {
                // Get public key
                var publicKey = keyStore.GetCertificate(keyName).PublicKey;
                // Get private key
                KeyStore.PrivateKeyEntry privateKey = (KeyStore.PrivateKeyEntry)keyStore.GetEntry(keyName, null);
                // Return a key pair
                return new KeyPair(publicKey, (IPrivateKey)privateKey.PrivateKey);
            }
            return null;
        }

        private Java.Security.Signature InitSignature(string keyName)
        {
            var keyPair = GetKeyPair(keyName);

            if (keyPair != null)
            {
                var signature = Java.Security.Signature.GetInstance(SIGNATURE_ALGORITHM);
                signature.InitSign(keyPair.Private);
                return signature;
            }
            return null;
        }

        /*
         * Before generating a key pair with biometric prompt, we need to check that the device supports fingerprint, iris, or face.
         * Currently, there are no FEATURE_IRIS or FEATURE_FACE constants on PackageManager.
         */
        private bool IsSupported
        {
            get
            {
                var packageManager = MainActivity.FormsContext.PackageManager;
                return packageManager.HasSystemFeature(PackageManager.FeatureFingerprint);
            }
        }

        class BiometricAuthenticationCallback : BiometricPrompt.AuthenticationCallback
        {
            public Action<BiometricPrompt.AuthenticationResult> Success;
            public Action Failed;
            public Action<BiometricAcquiredStatus, ICharSequence> Help;

            public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            {
                base.OnAuthenticationSucceeded(result);
                Success(result);
            }

            public override void OnAuthenticationFailed()
            {
                base.OnAuthenticationFailed();
                Failed();
            }

            public override void OnAuthenticationHelp([GeneratedEnum] BiometricAcquiredStatus helpCode, ICharSequence helpString)
            {
                base.OnAuthenticationHelp(helpCode, helpString);
                Help(helpCode, helpString);
            }
        }


        //Interface method to check this device is compatible to show biometricPrompt
        public bool CheckSDKGreater29()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                return true;
            else
                return false;
        }
    }

    class DialogInterfaceOnClickListener : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        private Action click;

        public DialogInterfaceOnClickListener(Action click)
        {
            this.click = click;
        }

        public void OnClick(IDialogInterface dialog, int which)
        {
            click();
        }
    }

}
