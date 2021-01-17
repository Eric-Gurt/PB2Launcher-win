using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Net;

using System.Runtime.InteropServices;
using System.Threading;

//using Gecko;
//using GeckoFX;

using System.Security.Cryptography;

namespace PB2Launcher
{
    public partial class Form1 : Form
    {
        //User created password will not be shorter than 6 characters.
        public static String MISSINGAUTHFILE = "N/A";
        private WebBrowser webBrowser1 = null;
        //private GeckoWebBrowser webBrowser1 = null;

        [DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(
            IntPtr hWnd,
                int hWndInsertAfter, // IntPtr
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );




        [DllImport("USER32.DLL")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

        [DllImport("user32.dll")]
        static extern IntPtr GetMenu(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        public static uint MF_BYPOSITION = 0x400;
        public static uint MF_REMOVE = 0x1000;

        public static void WindowsReStyle(Process proc)
        {
            //Process[] Procs = Process.GetProcesses();
            //foreach (Process proc in Procs)
            {

                // if (proc.ProcessName.StartsWith("notepad"))
                {
                    //get menu
                    IntPtr HMENU = GetMenu(proc.MainWindowHandle);
                    //get item count
                    int count = GetMenuItemCount(HMENU);
                    //loop & remove
                    for (int i = 0; i < count; i++)
                        RemoveMenu(HMENU, 0, (MF_BYPOSITION | MF_REMOVE));

                    //force a redraw
                    DrawMenuBar(proc.MainWindowHandle);
                }
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadImage(IntPtr hInst, string lpsz, IntPtr uType, IntPtr cxDesired, IntPtr cyDesired, IntPtr fuLoad);

        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);


        public Form1()
        {
            InitializeComponent();


            if (!Directory.Exists("data"))
            {
                MessageBox.Show("Try to unpack contents of this .zip archieve before executing it (application was not able to find 'data' folder near .exe file, usually it only happens when launcher is being executed in .zip archieve and not extracted).", 
                    "Unable to continue",
                                 MessageBoxButtons.OK,
                                 MessageBoxIcon.None);

                //this.Close();
                //return;
                //System.Windows.Forms.Application.Exit();
                System.Environment.Exit(0);
                return;
            }





            /*Gecko.Xpcom.Initialize(@"C:\_HomeData\PC\Projects C#\PB2Launcher\xulrunner");

            //Xpcom.Initialize("Firefox");
            webBrowser1 = new GeckoWebBrowser { Dock = DockStyle.Fill };
            //Form f = new Form();
            //f.Controls.Add(geckoWebBrowser);
            webBrowser1.Navigate("www.google.com");
            // Application.Run(f);
            */







            webBrowser1 = new WebBrowser();

            webBrowser1.Size = new Size(this.ClientSize.Width, this.ClientSize.Height);

            webBrowser1.AllowWebBrowserDrop = false;
            //webBrowser1.IsWebBrowserContextMenuEnabled = false;
            //webBrowser1.WebBrowserShortcutsEnabled = false;
            webBrowser1.ObjectForScripting = this;

            this.Controls.Add(webBrowser1);

            try
            {
                webBrowser1.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(OnDocumentCompleted);
                webBrowser1.Navigate("https://www.plazmaburst2.com/launcher/index.php");

                //webBrowser1.Navigate("about:blank");
                /*WebClient myWebClient_html_page = new WebClient();
                byte[] myDataBuffer_html_page = myWebClient_html_page.DownloadData("https://www.plazmaburst2.com/launcher/index.php");
                webBrowser1.Document.Write(String.Empty);
                webBrowser1.DocumentText = System.Text.Encoding.UTF8.GetString( myDataBuffer_html_page );*/
            }
            catch (System.UriFormatException)
            {
                File.WriteAllTextAsync("_error2.v", "URI format exception - could not navigate");

                /*try
                {
                    webBrowser1.Navigate("https://www.plazmaburst2.com/launcher/index.php");
                }
                catch(System.UriFormatException)
                {
                    File.WriteAllTextAsync("_error3.v", "URI format exception - could not navigate. But was able to proceed with regular string");
                }*/
                return;
            }  
            
            /* 
                Flow of this part of the code:
                1. Attempt to decrypt and retrieve password through getPlaintextPassword()

                If successful:
                2. Check if 3 days has passed, generate a new key if so

                Failing to decrypt it. (Invalid key: Possible sceanrios include: First time launching / PB2Launcher folder was shared..)
                - Generate a new key (at CryptographicException exception block)
            */
            //---------------------------------------------------------------------------------------------------------
            Aes aes = new Aes();
            try
            {
                //Step 1.
                //If missing .auth file, returns N/A.
                //If missing key file or invalid key, CryptographicException will be thrown.
                String plaintextPass = aes.getPlaintextPassword();

                //If missing .auth file, regenerate key and ask for login (jump into CryptographicException exception block).
                if(plaintextPass.Equals("N/A"))
                {
                    Console.WriteLine("\nMissing .auth file, throwing exception to regenerate key..");
                    throw new CryptographicException();
                }

                //Reaching here, decryption is successful. Plaintext of password is obtained.
                Console.WriteLine("Successful decryption.");

                //Step 2. Verify if key is not older than MAX_KEY_LIFE days.
                //If key is older, key will be regenerated without needing to reprompt login.
                aes.verifyAge(GetLogin(),plaintextPass);
            }
            //Only catch if it's an exception related to decryption.
            //Key is invalid, don't skip authentication and regenerate key.
            catch(CryptographicException)
            {
                Console.WriteLine("-----\nDecryption failed");
                //File.WriteAllTextAsync("_error5.v", "Exception during decryption; this indicates invalid key. Re-generating another key..\n" + ex.ToString());

                //By deleting both of these files before WindowsForm loads, user will have to reprompt credentials.
                File.Delete("data\\skip_auth.v");
                File.Delete("data\\Plazma Burst 2.auth");

                //Setting ForceRegenerate to 1, generating key even with key file.
                aes.setKey(1);
            }
            //This block of code should never happen.
            catch(Exception ex)
            {
                File.WriteAllTextAsync("errorFatal.txt","Fatal error, " + ex.ToString());
                Application.Exit();
            }
            //---------------------------------------------------------------------------------------------------------
        }
        
        /*private void webBrowser1_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            
            Debug.WriteLine(e.Url.ToString());
            //e.Url.ToString();
            //e.Cancel = true;
        }*/

        private bool IsOnline = true;

        private void OnDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {   

            try
            {
                WebBrowser webBrowser1 = (WebBrowser)sender;
                if (webBrowser1.ReadyState.Equals(WebBrowserReadyState.Complete))
                {
                    //webBrowser1.ShowPrintDialog();
                    // Debug.WriteLine("Complete. IsOffline: " +  );

                    if (webBrowser1.DocumentText.IndexOf("pb2_logo.png") == -1)
                    {
                        
                        // Take 2, for Windows 7 bug handling
                        WebClient myWebClient_html_page = new WebClient();
                        byte[] myDataBuffer_html_page = myWebClient_html_page.DownloadData("https://www.plazmaburst2.com/launcher/index.php");
                        webBrowser1.Document.Write(String.Empty);

                        string new_html = System.Text.Encoding.UTF8.GetString(myDataBuffer_html_page);

                        webBrowser1.DocumentText = new_html;

                        if (new_html.IndexOf("pb2_logo.png") == -1)
                        {


                            File.WriteAllTextAsync("_error1.v", "Contents of a page is following (has no 'pb2_logo.png' on page '" + webBrowser1.Url + "'): " + webBrowser1.DocumentText);

                            IsOnline = false;
                            RunGame("");
                        }
                    }
                }
                // else
                // Debug.WriteLine("State: " + webBrowser1.ReadyState.ToString() );
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                File.WriteAllTextAsync("_error4.v", "Exception during document complete (can be result of offline mode): " + ex.ToString() );

                IsOnline = false;
                RunGame("");
            }
        }

        public long GetLastUpdateServerTime()
        {
            if (File.Exists("data\\last_update.v"))
            {

                String text = File.ReadAllText("data\\last_update.v");

                return Int64.Parse(text);
            }
            return 0;
        }

        public bool UpdateGame()
        {
            try
            {
                WebClient myWebClient_time = new WebClient();
                byte[] myDataBuffer_time = myWebClient_time.DownloadData("https://www.plazmaburst2.com/launcher/time.php");

                WebClient myWebClient = new WebClient();
                byte[] myDataBuffer = myWebClient.DownloadData("https://www.plazmaburst2.com/pb2/pb2_re34.swf");
                File.WriteAllBytes("data\\pb2_re34_alt.swf", myDataBuffer);

                File.WriteAllBytes("data\\last_update.v", myDataBuffer_time);

                return true;
            }
            catch
            {
                return false;
            }
        }
        public void RunGame(String s)
        {
            //MessageBox.Show(message, "executed RunGame");

            string myparams = "?from_standalone=1";

            if (File.Exists("data\\Plazma Burst 2.auth"))
            {
                String login = File.ReadAllText("data\\Plazma Burst 2.auth").Split('\n', 2)[0];
                
                Aes aes = new Aes();
                String plaintextPass = aes.getPlaintextPassword();
                //Retrieved password, set object to null for garbage collection.
                aes = null;

                //File.WriteAllText("_debug.txt", "Password: " + plaintextPass);
                myparams = "?l=" + login + "&p=" + plaintextPass + "&from_standalone=1";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo("data\\flashplayer11_7r700_224_win_sa.exe");
            startInfo.WindowStyle = ProcessWindowStyle.Normal;

            if ( s == "" )
            startInfo.Arguments = "\"data\\pb2_re34_alt.swf" + myparams + "\"";
            else
            startInfo.Arguments = "\"data\\" + s + "\"";

            Process p = Process.Start(startInfo);

            p.WaitForInputIdle();

            int xx = this.Size.Width - this.ClientSize.Width;
            int yy = this.Size.Height - this.ClientSize.Height;

            if (s == "")
            SetWindowText(p.MainWindowHandle, "Plazma Burst 2");
            else
            if ( s == "plazma_burst_fttp.swf" )
            SetWindowText(p.MainWindowHandle, "Plazma Burst: Forward to the Past");

            WindowsReStyle(p);








            IntPtr m_pIcon;

            // Load the empty icon (from a file in this example)
            //string strIconFilePath = @"C:\_HomeData\PC\projects fl\PB2\publish exe 2\Plazma Burst 2\data\favicon.ico"; // Replace with a valid path on your system!

            string strIconFilePath = Directory.GetCurrentDirectory() + "\\data\\favicon.ico";

            const int IMAGE_ICON = 1;
            const int LR_LOADFROMFILE = 0x10;
            // edit: IntPtr pIcon = LoadImage(IntPtr.Zero, strIconFilePath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);
            m_pIcon = LoadImage(IntPtr.Zero, strIconFilePath, (IntPtr)IMAGE_ICON, (IntPtr)0, (IntPtr)0, (IntPtr)LR_LOADFROMFILE);

            SendMessage(p.MainWindowHandle, 0x80, IntPtr.Zero, m_pIcon);





            SetWindowPos(
                p.MainWindowHandle,
                0,
                    this.Left + this.Size.Width / 2 - 800 / 2 - xx / 2,
                    this.Top,
                    800 + xx,
                    400 + yy,
                0x0000
            //0x0001 // SWP_NOSIZE
            );

            if (this.WindowState == FormWindowState.Maximized )
            {
                ShowWindow(p.MainWindowHandle, SW_MAXIMIZE);
            }

           // Task.Delay( 1000 );

            this.Close();

            /*
            this.WindowState = FormWindowState.Minimized;

            p.WaitForExit();


            this.WindowState = FormWindowState.Normal;
            //this.BringToFront();
            this.Activate();


            //if (!IsOnline)
            {
                this.Close();
            }*/
        }
        public String GetLogin()
        {
            if (File.Exists("data\\Plazma Burst 2.auth"))
            {
                string text = File.ReadAllText("data\\Plazma Burst 2.auth");

                text = text.Split('\n', 2)[0];

                return text;
            }
            return "Guest";
        }
        //Retrieve password, static.
        public static String getPassword()
        {
            if (File.Exists("data\\Plazma Burst 2.auth"))
            {
                return File.ReadAllText("data\\Plazma Burst 2.auth").Split('\n', 2)[1];
            }
            return MISSINGAUTHFILE;
        }
        
        public void SaveLoginPassword(String l, String p)
        {
            Aes aes = new Aes();
            byte[] encryptedPass = aes.EncryptToByte(p);

            File.WriteAllText("data\\Plazma Burst 2.auth", l + '\n' + Convert.ToBase64String(encryptedPass));
        }
        public void DontAskForLogin()
        {
            File.WriteAllText("data\\skip_auth.v", "1");
        }
        public bool GetAskForLogin()
        {
            if (File.Exists("data\\skip_auth.v"))
                return false;

            return true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (webBrowser1 != null)
                webBrowser1.Size = new Size(this.ClientSize.Width, this.ClientSize.Height);
        }
        public void PopUpLink(String url)
        {
            //Process.Start(url);
            //System.Diagnostics.Process.Start(url);

            if (url.StartsWith("/"))
            {
                url = "https://www.plazmaburst2.com" + url;
            }


            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
