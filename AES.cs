using System;
using System.Text;
using System.Windows.Forms;
using System.IO;

using System.Security.Cryptography;

namespace PB2Launcher
{
    // Huge thanks to https://gist.github.com/magicsih/be06c2f60288b54d9f52856feb96ce8c for providing a base AES factory class for encryption and decryption
    // AES-128, CBC mode.
    public class Aes
    {
        private static RijndaelManaged rijndael = new RijndaelManaged();
        private static System.Text.UnicodeEncoding unicodeEncoding = new UnicodeEncoding();

        private const int CHUNK_SIZE = 128;

        private const int MAX_KEY_LIFE = 3;

        private void InitializeRijndael()
        {
            rijndael.Mode = CipherMode.CBC;
            rijndael.Padding = PaddingMode.PKCS7;            
        }

        //constructor without argument, generates a random key in the appdata of userprofile.
        public Aes()
        {
            InitializeRijndael();
            
            rijndael.KeySize = CHUNK_SIZE;
            rijndael.BlockSize = CHUNK_SIZE;

            //invoke wrapper function around rijndael.GenerateKey(), setting key and iv.
            setKey();
        }

        //constructor with argument, taking a key as the first argument and IV as the second. (base64)
        //not needed
        /*
        public Aes(String base64key, String base64iv)
        {
            InitializeRijndael();

            rijndael.Key = Convert.FromBase64String(base64key);
            rijndael.IV = Convert.FromBase64String(base64iv);    
        }
        */

        //constructor with argument, taking a key as the first argument and IV as the second. (binary) 
        //not needed
        /*
        public Aes(byte[] key, byte[] iv)
        {
            InitializeRijndael();

            rijndael.Key = key;
            rijndael.IV = iv;
        }
        */

        public string Decrypt(byte[] cipher)
        {
            ICryptoTransform transform = rijndael.CreateDecryptor();            
            byte[] decryptedValue = transform.TransformFinalBlock(cipher, 0, cipher.Length);
            return unicodeEncoding.GetString(decryptedValue);
        }

        //Not used.
        /*
        public string DecryptFromBase64String(string base64cipher)
        {
            return Decrypt(Convert.FromBase64String(base64cipher));
        }
        */
              
        public byte[] EncryptToByte(string plain)
        {
            ICryptoTransform encryptor = rijndael.CreateEncryptor();
            byte[] cipher = unicodeEncoding.GetBytes(plain);
            byte[] encryptedValue = encryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            Console.WriteLine(Convert.ToBase64String(encryptedValue));
            return encryptedValue;
        }

        //Not used
        /*
        public string EncryptToBase64String(string plain)
        {
            return Convert.ToBase64String(EncryptToByte(plain));
        }
        */
        
        public string GetKey()
        {
            return Convert.ToBase64String(rijndael.Key);
        }

        public string GetIV()
        {
            return Convert.ToBase64String(rijndael.IV);
        }

        public override string ToString()
        {
            return "KEY:" + GetKey() + Environment.NewLine + "IV:" + GetIV();
        }

        //For every AES object, constructor will call this function.
        //Reads key from key file. If key file is missing, generate a new random key and create the file.
        //If ForceRegenerate is set to 1, regenerate key even if file exist.
        public void setKey(int ForceRegenerate = 0)
        {
            //Filepath to store the key.
            //https://stackoverflow.com/questions/9993561/c-sharp-open-file-path-starting-with-userprofile
            string folderWithEnv = @"%USERPROFILE%\AppData\Local\.plazmaburst2";
            string folderPath = Environment.ExpandEnvironmentVariables(folderWithEnv);
            string filePath = folderPath + "\\key";

            //If missing key file or if ForceRegenerate is == 1
            if(!File.Exists(filePath) || ForceRegenerate == 1)
            {
                if(ForceRegenerate == 1)
                {
                    Console.WriteLine("\nGenerating a new key..");
                }
                else
                {
                    Console.WriteLine("\nKey file not found, generating a new one..");
                }

                DirectoryInfo di = Directory.CreateDirectory(folderPath);
                //Setting directory to hidden
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden; 

                var myFile = File.Create(filePath);
                myFile.Close();

                //Generates a AES key.
                rijndael.GenerateKey();
                //Key is now stored in Aes.key, converting it to base64
                string Base64AESKey = Convert.ToBase64String(rijndael.Key);

                //Generate IV for that key
                rijndael.GenerateIV();
                string Base64IV = Convert.ToBase64String(rijndael.IV);

                String[] KeyAndIv = new string[] {Base64AESKey, Base64IV};
                //Store the key and IV in the user profile's appdata folder.
                File.WriteAllLines(filePath,KeyAndIv);

                Console.WriteLine("New key: " + Base64AESKey);
                Console.WriteLine("New IV: " + Base64IV);

                //Writes the keyAge.v file, indicating the time the key is created.
                writeKeyAgeFile();
            }
            //If key file is present
            else
            {
                Console.WriteLine("\nKey file found, reading from it..");
                //Reading key and iv, seperated by \n
                String[] keyAndIv = File.ReadAllText(filePath).Split("\n");

                String key = keyAndIv[0];
                String IV = keyAndIv[1];

                //Convert from base64 forms into binaries and setting the attributes of the AES object.
                rijndael.Key = Convert.FromBase64String(key);
                rijndael.IV = Convert.FromBase64String(key);

                Console.WriteLine("Current key: " + key);
                Console.WriteLine("Current IV: " + IV);
            }

        }

        //Returns password in plaintext. Throws CryptographicException if decryption fails. (Most likely invalid key.)
        public String getPlaintextPassword()
        {
            //Accessing a public static function.
            String password = Form1.getPassword();
            if(password.Equals(Form1.MISSINGAUTHFILE))
            {
                return Form1.MISSINGAUTHFILE;
            }

            byte[] passwordInBytes = Convert.FromBase64String(password);
            Console.WriteLine("\nDecrypting..\n---\nUsing key: " + GetKey());
            Console.WriteLine("Decrypting: " + password);
            string plaintextPass = Decrypt(passwordInBytes);

            return plaintextPass;
        }

        //Function which creates a keyAge file when key is generated, representing the time the key is generated.
        public void writeKeyAgeFile(){
            String filePath = "data\\keyAge.v";

            //Get current localtime
            DateTime localDate = DateTime.Now;
            File.WriteAllText(filePath, localDate.ToString());
        }

        //Verify age will regenerate key if key is older than MAX_KEY_LIFE days, without asking for user to relogin.
        //To prevent reprompt, login and plaintext password needs to be saved to be used to encrypted with the new key, recreating the .auth file.
        //Day of the year will be used to determine key age.
        //A file, keyAge stored at the data folder, will store the time key is generated.
        public void verifyAge(String login, String plaintextPass)
        {
            String filePath = "data\\keyAge.v";

            //If file does not exist, regenerate new key and override .auth file.
            if(!File.Exists(filePath))
            {
                Console.WriteLine("\nMissing keyAge file, force key regeneration..");
                regenerateKeyWithoutLoginPrompt(login, plaintextPass);
                return;
            }

            String date = File.ReadAllText(filePath);

            //Tries to parse the keyAge.v file
            try
            {
                DateTime keyStartDate = DateTime.Parse(date);
                //Parse is successfully, now checking if key is older than MAX_KEY_AGE days.

                //Get current localtime
                DateTime currentDate = DateTime.Now;
                //Get duration between key creation date and current date
                TimeSpan keyAge = currentDate - keyStartDate;
                int DayDifference = keyAge.Days;

                if(DayDifference > MAX_KEY_LIFE)
                {
                    Console.WriteLine("\nKey older than " + MAX_KEY_LIFE + " days, regenerating key..");
                    regenerateKeyWithoutLoginPrompt(login, plaintextPass);
                }
            }
            //Catch poorly formatted file, regenerate the key in this case.
            catch(FormatException)
            {
                Console.WriteLine("\nPoorly formatted keyAge file, regenerating key...");
                regenerateKeyWithoutLoginPrompt(login, plaintextPass);
            }
            //This block of code should never happen.
            catch(Exception er){
                File.WriteAllTextAsync("errorFatal.txt","Fatal error, " + er.ToString());
                Application.Exit();
            }
        }

        //Regenerates key without needing user to relogin again.
        public void regenerateKeyWithoutLoginPrompt(String login, String plaintextPass)
        {
            //Force regenerate a key
            setKey(1);
            //Encrypt plaintextpass with new key.
            byte[] encryptedPass = EncryptToByte(plaintextPass);
            //Override .auth file with newly encrypted pass.
            File.WriteAllText("data\\Plazma Burst 2.auth", login + '\n' + Convert.ToBase64String(encryptedPass));
        }
        
    }
}