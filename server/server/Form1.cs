using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.IO;
namespace server
{
    public partial class Form1 : Form
    {

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> clientSockets = new List<Socket>();
        List <string> connected_usernames = new List<string>();
        bool terminating = false;
        bool listening = false;
        string path = string.Empty;







        private bool checkusername(string username)
        {
            bool exists = false;
            for (int i = 0; i < connected_usernames.Count; i++)
            {
                if (connected_usernames[i] == username)
                {
                    exists = true;
                }
            }

            return exists;
        }



        private int get_file_count(string directory) // COUNT HOW MANY OF THIS FILE EXISTS IN USER'S FOLDER ALREADY
        {
            int file_count = -1;
            int length = directory.Substring(0, directory.IndexOf(".txt")).Length;


            if(directory.IndexOf('(') != -1)
            {
                length = directory.Substring(0, directory.IndexOf('(')).Length;
            }


            while (File.Exists(directory))
            {
                file_count += 1;

                directory = directory.Substring(0, length);

                directory += "(" + file_count.ToString() + ").txt";
            }

            return file_count;
        }









        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }









        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if(Int32.TryParse(textBox_port.Text, out serverPort))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort); // started listening to port
                serverSocket.Bind(endPoint);
                serverSocket.Listen(100);

                listening = true;
                button_listen.Enabled = false;

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                logs.AppendText("\nStarted listening on port: " + serverPort + "\n");

            }
            else
            {
                logs.AppendText("Please check port number \n");
            }
        }










        private void Accept()
        {
            while(listening)
            {
                
                try
                {
                    Socket newClient = serverSocket.Accept();
                    clientSockets.Add(newClient);
                                        
                    Thread receiveThread = new Thread(() => ReceiveTCP(newClient)); // updated
                    receiveThread.Start();
                     
                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        logs.AppendText("The socket stopped working.\n");
                    }

                }
            }
        }







       public void ReceiveTCP(Socket client)
        {


            bool connected = true;
            int BufferSize = 64;







            // GET USERNAME
            Byte [] usernamebyte = new Byte [BufferSize]; 
            client.Receive(usernamebyte);        
            string username = Encoding.Default.GetString(usernamebyte);
            username = username.Replace("\0", string.Empty);

            






            // CHECK IF USERNAME ALREADY CONNECTED
            bool same_user = false; // if user already connected, 2nd coming user shouldn't delete connected user
            if (checkusername(username))
            {
                string send_message = "Disconnect";
                Byte[] buffer = Encoding.Default.GetBytes(send_message);
                client.Send(buffer);
                logs.AppendText("User " + username + " is already connected. Connection failed.\n");

                Thread.Sleep(200);
                client.Close();
                same_user = true;
                connected = false;
            }
            else
            {
                string send_message = "Connect";
                Byte[] buffer = Encoding.Default.GetBytes(send_message);
                client.Send(buffer);

                string msg = "User " + username + " has connected.\n";
                connected_usernames.Add(username);
                logs.AppendText(msg);
            }







            // DEFINE DIRECTORY AND CHECK IF DIRECTORY ALREADY EXISTS

            username = username.Replace("\0", string.Empty);
            string dir = path + "\\" + username;

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }






            // NOTE: garbage buffers are there for flow control, to check if we get Done or Disconnect

            
            int same_filename_count = 0;
            while (connected)
            {
                Byte[] garbage1 = new byte[BufferSize];
                try { client.Receive(garbage1); }
                catch { }

                string garbage = Encoding.Default.GetString(garbage1);
                garbage = garbage.Replace("\0", string.Empty);


                if(garbage == "Disconnect")
                {
                    connected = false;
                    break;
                }
                

                // GET THE INSTRUCTION TO BE EXECUTED

                Byte[] _instruction = new byte[BufferSize];
                try
                {
                    client.Receive(_instruction);
                }
                catch { } 
                string instruction = Encoding.Default.GetString(_instruction);
                instruction = instruction.Replace("\0", string.Empty);

                



                // IF USER DISCONNECTS

                if(instruction == "Disconnect")
                {
                    connected = false;
                    break;
                }

                // IF USER WANTS TO UPLOAD A FILE TO SERVER

                else if (instruction == "UploadFile")
                {

                    Byte[] filenamebyte = new Byte[BufferSize];
                    string filename = "";

                    if (garbage == "Done")
                    {
                        try
                        {
                            client.Receive(filenamebyte);
                        }
                        catch { }

                        filename = Encoding.Default.GetString(filenamebyte);
                        filename = filename.Replace("\0", string.Empty);
                    }
                    if (garbage == "Disconnect" || filename == "Disconnect")
                    {
                        connected = false;
                        break;
                    }





                    System.IO.FileStream Fs = null;

                    // CREATE A FILE WITH SEND FILENAME TO THE SELECTED DIRECTORY
                    if (!File.Exists(dir + "\\" + filename))
                    {
                        Console.WriteLine(dir + "\\" + filename);
                        Fs = new System.IO.FileStream(dir + "\\" + filename, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                    }

                    else
                    {
                        same_filename_count = get_file_count(dir + "\\" + filename);
                        Fs = new System.IO.FileStream(dir + "\\" + filename.Substring(0, filename.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                        filename = filename.Substring(0, filename.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt";
                    }




                    // START READING THE FILE 
                    bool reading = true;
                    logs.AppendText(username + " has started uploading the file named " + filename + "\n");


                    while (connected && !terminating && reading)

                    {
                        string actualcontent, garbage2_str;
                        Byte[] content = new Byte[1048576]; // 1MB packets
                        Byte[] garbage2 = new Byte[BufferSize];


                        try
                        {   // RECEIVE THE FILE CONTENT

                            client.Receive(garbage2);
                            garbage2_str = Encoding.Default.GetString(garbage2);
                            garbage2_str = garbage2_str.Replace("\0", string.Empty);

                            if (garbage2_str == "Disconnect")
                            {

                                connected = false;
                                break;
                            }




                            client.Receive(content);
                            actualcontent = Encoding.Default.GetString(content);
                            actualcontent = actualcontent.Replace("\0", string.Empty);

                            if(actualcontent != "FileDone")
                            {
                                Byte[] msg = new Byte[5];
                                string message = "GotIt";
                                msg = Encoding.Default.GetBytes(message);
                                client.Send(msg); // Send acknowledgment that server has received the packet
                            }
                            


                            string file_path = dir + "\\" + filename;
                            Fs.Close();
                            using (StreamWriter sw = File.AppendText(file_path))
                            {

                                if (actualcontent == "Disconnect") connected = false;
                                else if (actualcontent == "FileDone") // If all file send process is finished
                                {
                                    reading = false;
                                    logs.AppendText(username + ", uploaded the file named " + filename + "\n");
                                }
                                else // If packet send process is finisjed
                                {
                                    sw.Write(actualcontent);
                                }
                            }
                        }


                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }





                // IF USER WANTS TO GET HIS/HER FILES FROM THE SERVE

                else if (instruction == "GetFileList")
                {
                    string[] files = Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories);

                    foreach (string file_path in files)
                    {
                        FileInfo file_info = new FileInfo(file_path);
                        string file_information = file_info.Name + " / " + file_info.Length + " Bytes / " + file_info.CreationTime;

                        Byte[] file = new Byte[256];
                        file = Encoding.Default.GetBytes(file_information);
                        client.Send(file);


                        Byte[] done_ack = new Byte[4];
                        client.Receive(done_ack);
                    }

                    // SEND DONE ACKNOWLEDGEMENT
                    Byte[] ack = new Byte[4];
                    string ack_msg = "Done";
                    ack = Encoding.Default.GetBytes(ack_msg);
                    client.Send(ack);

                    logs.AppendText(username + " requested his/her file list.\n");
                }






                // IF USER WANTS TO GET ALL THE PUBLIC FILES

                else if (instruction == "GetPublicFileList")
                {
                    string dir_public = path + "\\Public";

                    string[] files = Directory.GetFiles(dir_public, "*.txt", SearchOption.AllDirectories);

                    foreach (string file_path in files)
                    {
                        FileInfo file_info = new FileInfo(file_path);
                        string file_information = file_info.Name.Substring(0, file_info.Name.IndexOf("_")) + " / " + file_info.Name.Substring(file_info.Name.IndexOf("_") + 1) + "/" + file_info.Length + " Bytes / " + file_info.CreationTime;

                        Byte[] file = new Byte[256];
                        file = Encoding.Default.GetBytes(file_information);
                        client.Send(file);


                        Byte[] done_ack = new Byte[4];
                        client.Receive(done_ack);
                    }


                    // DONE ACK.
                    Byte[] ack = new Byte[4];
                    string ack_msg = "Done";
                    ack = Encoding.Default.GetBytes(ack_msg);
                    client.Send(ack);

                    logs.AppendText(username + " requested the public file list.\n");
                }






                // IF USER WANTS TO MAKE ONE OF HIS/HER FILES PUBLIC

                else if (instruction == "MakePublic")
                {
                    Byte[] filename_to_move = new byte[BufferSize];
                    client.Receive(filename_to_move);
                    string filename_to_public = Encoding.Default.GetString(filename_to_move);
                    filename_to_public = filename_to_public.Replace("\0", string.Empty);

                    string get_file_dir = dir + "\\" + filename_to_public;

                    if (File.Exists(get_file_dir))
                    {
                        string check_public_dir = path + "\\Public\\" + username + "_" + filename_to_public;

                        // IS IT ALREADY A PUBLIC FILE
                        if (!File.Exists(check_public_dir))
                        {
                            string public_dir = path + "\\Public\\" + username + "_" + filename_to_public;
                            File.Copy(get_file_dir, public_dir);
                            string message = "File " + filename_to_public + " has been made public.";
                            Byte[] message_buff = Encoding.Default.GetBytes(message);
                            client.Send(message_buff);

                            logs.AppendText(username + " made his/her file named " + filename_to_public + " public.\n");
                        }

                        else
                        {
                            string message = "File " + filename_to_public + " is already a public file.";
                            Byte[] message_buff = Encoding.Default.GetBytes(message);
                            client.Send(message_buff);

                            logs.AppendText(username + " tried to make a file public that is already public.\n");
                        }
                    }

                    else
                    {
                        string message = "File " + filename_to_public + " does not exist in server.";
                        Byte[] message_buff = Encoding.Default.GetBytes(message);
                        client.Send(message_buff);

                        logs.AppendText(username + " tried to make a file that does not exist public.\n" );
                    }
                }








                // IF USER WANTS TO DELETE AN EXISTING FILE FROM THE SERVER

                else if (instruction == "DeleteFile")
                {
                    Byte[] filename_to_delete_buff = new byte[BufferSize];
                    client.Receive(filename_to_delete_buff);
                    string filename_to_delete = Encoding.Default.GetString(filename_to_delete_buff);
                    filename_to_delete = filename_to_delete.Replace("\0", string.Empty);

                    string private_dir = dir + "\\" + filename_to_delete;

                    if (File.Exists(private_dir))
                    {
                        File.Delete(private_dir);

                        string public_dir = path + "\\Public\\" + username + "_" + filename_to_delete;

                        if (File.Exists(public_dir))
                        {
                            File.Delete(public_dir);
                        }


                        string message = "File " + filename_to_delete + " deleted.";
                        Byte[] message_buff = Encoding.Default.GetBytes(message);
                        client.Send(message_buff);

                        logs.AppendText(username + " deleted the file named " + filename_to_delete + ".\n");
                    }

                    else
                    {
                        string message = "You do not have a file named " + filename_to_delete + ".";
                        Byte[] message_buff = Encoding.Default.GetBytes(message);
                        client.Send(message_buff);

                        logs.AppendText(username + " tried to delete a file that does not exist.\n");
                    }
                }








                // IF USER WANTS TO A COPY A FILE

                else if(instruction == "CopyFile")
                {
                    Byte[] filename_to_copy_buff = new byte[BufferSize];
                    client.Receive(filename_to_copy_buff);
                    string filename_to_copy = Encoding.Default.GetString(filename_to_copy_buff);
                    filename_to_copy = filename_to_copy.Replace("\0", string.Empty);


                    string private_dir = dir + "\\" + filename_to_copy;

                    if (File.Exists(private_dir))
                    {
                        int count = get_file_count(private_dir);
                        string copy_dir;

                        if (filename_to_copy.IndexOf('(') == -1)
                        {
                            copy_dir = dir + "\\" + filename_to_copy.Substring(0, filename_to_copy.IndexOf(".txt")) + "(" + count.ToString() + ").txt";
                            File.Copy(private_dir, copy_dir);
                        }

                        else
                        {
                            copy_dir = dir + "\\" + filename_to_copy.Substring(0, filename_to_copy.IndexOf("(")) + "(" + count.ToString() + ").txt";
                            File.Copy(private_dir, copy_dir);
                        }


                        string public_dir = path + "\\Public\\" + username + "_" + filename_to_copy;

                        if (File.Exists(public_dir))
                        {
                            if (filename_to_copy.IndexOf('(') == -1)
                            {
                                copy_dir = path + "\\Public\\" + username + "_" + filename_to_copy.Substring(0, filename_to_copy.IndexOf(".txt")) + "("+ count.ToString() +").txt";
                                File.Copy(public_dir, copy_dir);
                            }

                            else
                            {
                                copy_dir = path + "\\Public\\" + username + "_" + filename_to_copy.Substring(0, filename_to_copy.IndexOf("(")) + "(" + count.ToString() + ").txt";
                                File.Copy(public_dir, copy_dir);
                            }
                        }


                        string message = "File " + filename_to_copy + " has been copied.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " copied a file named " + filename_to_copy + ".\n");
                    }


                    else
                    {
                        string message = "File " + filename_to_copy + " can not be copied. You do not have such a file.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " tried to copy a file that does not exist.\n");
                    }  
                }












                // IF USER WANTS TO DOWNLOAD A FILE FROM HIS/HER PRIVATE FOLDER

                else if (instruction == "DownloadPrivate")
                {
                    Byte[] filename_to_download_buff = new byte[BufferSize];
                    client.Receive(filename_to_download_buff);
                    string filename_to_download = Encoding.Default.GetString(filename_to_download_buff);
                    filename_to_download = filename_to_download.Replace("\0", string.Empty);
                    

                    

                    string download_path = dir + "\\" + filename_to_download;

                    if (File.Exists(download_path))
                    {
                        Byte[] buffDone = new byte[4];
                        string done_msg = "Done";
                        buffDone = Encoding.Default.GetBytes(done_msg);
                        client.Send(buffDone);
                        Thread.Sleep(200);


                        Stream fileStream = File.OpenRead(download_path);
                        Byte[] fileBuffer = new byte[fileStream.Length];
                        fileStream.Read(fileBuffer, 0, (int)fileStream.Length);

                        
                        Byte[] sendBuffer = new byte[1048576];  //send the files in packets of 1 MB
                        int pos = 0;
                        long remainingFileLength = fileStream.Length;


                        //If the file is larger than 1MB, we will send multiple 1MB files to send the whole file
                        
                        while (remainingFileLength >= 1048576)
                        {
                            for (int i = 0; i < 1048576; i++)
                            {
                                sendBuffer[i] = fileBuffer[pos];
                                pos++;
                            }
                            client.Send(sendBuffer);
                            remainingFileLength -= 1048576;

                            Byte[] doneBuffer = new byte[4];
                            client.Receive(doneBuffer);
                        }


                        // either the file is less than 1MB or after multiple 1MB packets, if there is remaining data to be sent that is less than 1MB
                       
                        if (remainingFileLength > 0)
                        {
                            for (int i = 0; i < remainingFileLength; i++)
                            {
                                sendBuffer[i] = fileBuffer[pos];
                                pos++;
                            }
                            for (int i = (int)remainingFileLength; i < 1048576; i++)
                            {
                                sendBuffer[i] = Encoding.Default.GetBytes("\0")[0];
                            }
                            client.Send(sendBuffer);

                            Byte[] doneBuffer = new byte[4];
                            client.Receive(doneBuffer);
                        }



                        Thread.Sleep(200);
                        string message = "The file " + filename_to_download + " has been succesfully downloaded from your private folder.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " downloaded the file " + filename_to_download + " from his/her private folder.\n");
                    }

                    else
                    {
                        string message = "File " + filename_to_download + " can not be downloaded. You do not have such a file.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " tried to download a private file that does not exist.\n");
                    }                   
                }






                // IF USER WANTS TO DOWNLOAD A FILE FROM PUBLIC

                else if(instruction == "DownloadPublic")
                {
                    Byte[] owner_buff = new byte[BufferSize];
                    client.Receive(owner_buff);
                    string owner = Encoding.Default.GetString(owner_buff);
                    owner = owner.Replace("\0", string.Empty);


                    Byte[] filename_to_download_buff = new byte[BufferSize];
                    client.Receive(filename_to_download_buff);
                    string filename_to_download = Encoding.Default.GetString(filename_to_download_buff);
                    filename_to_download = filename_to_download.Replace("\0", string.Empty);


                    string download_path = path + "\\Public\\" + owner + "_" + filename_to_download;

                    if (File.Exists(download_path))
                    {
                        Byte[] buffDone = new byte[4];
                        string done_msg = "Done";
                        buffDone = Encoding.Default.GetBytes(done_msg);
                        client.Send(buffDone);
                        Thread.Sleep(200);


                        Stream fileStream = File.OpenRead(download_path);
                        Byte[] fileBuffer = new byte[fileStream.Length];
                        fileStream.Read(fileBuffer, 0, (int)fileStream.Length);


                        Byte[] sendBuffer = new byte[1048576];  //send the files in packets of 1 MB
                        int pos = 0;
                        long remainingFileLength = fileStream.Length;


                        //If the file is larger than 1MB, we will send multiple 1MB files to send the whole file

                        while (remainingFileLength >= 1048576)
                        {
                            for (int i = 0; i < 1048576; i++)
                            {
                                sendBuffer[i] = fileBuffer[pos];
                                pos++;
                            }
                            client.Send(sendBuffer);
                            remainingFileLength -= 1048576;

                            Byte[] doneBuffer = new byte[4];
                            client.Receive(doneBuffer);
                        }


                        // either the file is less than 1MB or after multiple 1MB packets, if there is remaining data to be sent that is less than 1MB

                        if (remainingFileLength > 0)
                        {
                            for (int i = 0; i < remainingFileLength; i++)
                            {
                                sendBuffer[i] = fileBuffer[pos];
                                pos++;
                            }
                            for (int i = (int)remainingFileLength; i < 1048576; i++)
                            {
                                sendBuffer[i] = Encoding.Default.GetBytes("\0")[0];
                            }
                            client.Send(sendBuffer);

                            Byte[] doneBuffer = new byte[4];
                            client.Receive(doneBuffer);
                        }


                        Thread.Sleep(200);
                        string message = "The file " + filename_to_download + " with owner " + owner + " has been succesfully downloaded from the public folder.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " downloaded the file " + filename_to_download + " with owner " + owner + " from the public folder.\n");
                    }

                    else
                    {
                        string message = "File " + filename_to_download + " can not be downloaded. Either username or filename is incorrect.";
                        Byte[] message_buffer = new Byte[128];
                        message_buffer = Encoding.Default.GetBytes(message);
                        client.Send(message_buffer);

                        logs.AppendText(username + " tried to download a public file that does not exist.\n");
                    }
                }
            }




            // IF THREAD DID NOT COME TO THIS POINT DUE TO ONE USER TRYING TO ACCES WHILE STILL CONNECTED
            if (!same_user)
            {
                for (int i = 0; i < connected_usernames.Count; i++)
                {
                    if (connected_usernames[i] == username)
                    {
                        connected_usernames.RemoveAt(i); // Delete user
                    }
                }
                logs.AppendText(username + " has disconnected.\n");
            }
        }










        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            string disconnect_msg = "Disconnect";
            Byte[] buffer = Encoding.Default.GetBytes(disconnect_msg);

            foreach (Socket client in clientSockets)
            {
                try
                {
                    client.Send(buffer);
                }
                catch
                {
                    logs.AppendText("There is a problem! Check the connection...\n");
                }

            }

            foreach (Socket client in clientSockets)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    logs.AppendText("There is a problem! Check the connection...\n");
                }

            }



            listening = false;
            terminating = true;
            Environment.Exit(0);
        }







        private void button_directory_Click(object sender, EventArgs e)
        {
            string SaveFileName = string.Empty;
           

            FolderBrowserDialog DialogSave = new FolderBrowserDialog();
            DialogSave.ShowDialog();

            path = DialogSave.SelectedPath;
            logs.AppendText(path);
            
                
        }
    }
}
