using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography.DataProtection;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage.Streams;

namespace MetroUsageMeter
{
    sealed class CredentialsEncrypter
    {
        // Encryption and decryption parameters
        private string cipherAlgName;
        private string ivString;
        private uint algBlockSizeInBytes;

        // Key derivation parameters
        private string kdfSaltString;
        private string kdfAlgNameString;
        private uint kdfIterationCount;

        private string password;

        public CredentialsEncrypter() : this("REDACTED", "REDACTED", "REDACTED") { }

        public CredentialsEncrypter(string password, string iv, string salt)
        {
            // Initialize the encryption and decryption parameters.
            this.cipherAlgName = SymmetricAlgorithmNames.AesCbcPkcs7;
            this.ivString = iv;
            this.algBlockSizeInBytes = 16;

            // Initialize the key derivation parameters.
            this.kdfSaltString = salt;
            this.kdfAlgNameString = KeyDerivationAlgorithmNames.Pbkdf2Sha256;
            this.kdfIterationCount = 10000;

            this.password = password;
        }

        public String Encrypt(String input)
        {
            String encryptedDataString = null;
            try
            {

                // Derive a key from the password.
                IBuffer derivedKeyBuffer = DeriveKeyFromPassword();

                // Convert the initialization vector string to binary.
                IBuffer ivBuffer = CryptographicBuffer.ConvertStringToBinary(ivString, BinaryStringEncoding.Utf8);

                // Encrypt the input.
                IBuffer encryptedDataBuffer = EncryptDataBuffer(derivedKeyBuffer, input, cipherAlgName, ivBuffer);
                encryptedDataString = CryptographicBuffer.EncodeToBase64String(encryptedDataBuffer);
            }
            catch (Exception e)
            {
                // Ignore errors;
            }
            return encryptedDataString;
        }

        public String Decrypt(String input)
        {
            String decryptedDataString = null;
            try
            {
                // Derive a key from the password.
                IBuffer derivedKeyBuffer = DeriveKeyFromPassword();

                // Convert the initialization vector string to binary.
                IBuffer ivBuffer = CryptographicBuffer.ConvertStringToBinary(ivString, BinaryStringEncoding.Utf8);

                // Decrypt the input.
                IBuffer decryptedDataBuffer = DecryptDataBuffer(derivedKeyBuffer, input, cipherAlgName, ivBuffer);
                if (decryptedDataBuffer == null)
                {
                    return null;
                }
                decryptedDataString = CryptographicBuffer.ConvertBinaryToString(Windows.Security.Cryptography.BinaryStringEncoding.Utf8, decryptedDataBuffer);
            }
            catch (Exception e)
            {
                // Ignroe errors;
            }
            return decryptedDataString;
        }

        // Derive a cryptographic key from a password.
        private IBuffer DeriveKeyFromPassword()
        {
            IBuffer derivedKeyBuffer = null;

            // Convert the input to binary.
            IBuffer secret = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);

            // Initialize the password-based key derivation function (PBKDF2) parameters.
            IBuffer salt = CryptographicBuffer.ConvertStringToBinary(kdfSaltString, BinaryStringEncoding.Utf8);
            KeyDerivationParameters pbkdf2Params = KeyDerivationParameters.BuildForPbkdf2(salt, kdfIterationCount);

            // Open the PBKDF2_SHA256 algorithm provider.
            KeyDerivationAlgorithmProvider algorithmProvider = KeyDerivationAlgorithmProvider.OpenAlgorithm(kdfAlgNameString);

            // Create a secret key.
            CryptographicKey secretKey = algorithmProvider.CreateKey(secret);

            // Peform the derivation.
            derivedKeyBuffer = Windows.Security.Cryptography.Core.CryptographicEngine.DeriveKeyMaterial(secretKey, pbkdf2Params, algBlockSizeInBytes);

            return derivedKeyBuffer;
        }

        // Encrypt a data buffer.
        private IBuffer EncryptDataBuffer(IBuffer derivedKeyBuffer, String stringToEncrypt, String algNameString, IBuffer ivBuffer)
        {
            IBuffer encryptedBuffer = null;

            // Convert the input string, stringToEncrypt, to binary.
            IBuffer inputDataBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(stringToEncrypt, BinaryStringEncoding.Utf8);

            // Open the algorithm provider specified by the algNameString input parameter.
            SymmetricKeyAlgorithmProvider algorithmProvider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(algNameString);

            // Create a symmetric key.
            CryptographicKey symmetricKey = algorithmProvider.CreateSymmetricKey(derivedKeyBuffer);

            // Encrypt the input string.
            encryptedBuffer = CryptographicEngine.Encrypt(symmetricKey, inputDataBuffer, ivBuffer);

            return encryptedBuffer;
        }

        // Decrypt a data buffer.
        private IBuffer DecryptDataBuffer(IBuffer derivedKeyBuffer, String stringToDecrypt, String algNameString, IBuffer ivBuffer)
        {
            IBuffer decryptedBuffer = null;
            try
            {
                // Convert the input string, stringToDecrypt, to binary.
                IBuffer inputDataBuffer = CryptographicBuffer.DecodeFromBase64String(stringToDecrypt);

                // Open the algorithm provider specified by the algNameString input parameter.
                SymmetricKeyAlgorithmProvider algorithmProvider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(algNameString);

                // Create a symmetric key.
                CryptographicKey symmetricKey = algorithmProvider.CreateSymmetricKey(derivedKeyBuffer);

                // Decrypt the input string.
                decryptedBuffer = CryptographicEngine.Decrypt(symmetricKey, inputDataBuffer, ivBuffer);
            }
            catch (Exception e)
            {
                // Ignore decryption errors
            }
            return decryptedBuffer;
        }
    }
}
