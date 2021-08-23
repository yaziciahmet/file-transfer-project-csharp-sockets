using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;

        private int get_file_count(string directory) // COUNT HOW MANY OF THIS FILE EXISTS IN USER'S FOLDER ALREADY
        {
            int file_count = -1;
            int length = directory.Substring(0, directory.IndexOf(".txt")).Length;


            if (directory.IndexOf('(') != -1)
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

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {//Connect Button
            if (textBoxUsername.Text != "Disconnect" && textBoxUsername.Text != "Done" && textBoxUsername.Text != "")
            {//Disconnect and Done are predefined keywords in our server-client communication.
                //Connect Button
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                string IP = textBoxIP.Text;

                int portNum;
                if (Int32.TryParse(textBoxPort.Text, out portNum))
                {
                    try
                    {
                        //Connect to the server
                        clientSocket.Connect(IP, portNum);
                        buttonConnect.Enabled = false;
                        
                        //send the username to the server
                        string username = textBoxUsername.Text;
                        if (username != "" && username.Length <= 64)
                        {
                            Byte[] bufferUsername = new byte[64];
                            bufferUsername = Encoding.Default.GetBytes(username);
                            clientSocket.Send(bufferUsername);
                        }
                        
                        //receive the reply from the server
                        Byte[] receiveBuffer = new byte[64];
                        clientSocket.Receive(receiveBuffer);
                        string incomingMessage = Encoding.Default.GetString(receiveBuffer);
                        incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                        //send Done message to let the server know that you are done
                        string done = "Done";
                        if (done != "" && done.Length <= 64)
                        {
                            Byte[] bufferDone = new byte[64];
                            bufferDone = Encoding.Default.GetBytes(done);
                            clientSocket.Send(bufferDone);
                        }

                        //if the username is already connected to the server, it will send Disconnect message, otherwise it will send Connect message
                        if (incomingMessage == "Disconnect")
                        {//The username is already connected to the server
                            Logs.AppendText("A user with username " + username + " is already connected to the server.\n");
                            connected = false;
                            buttonConnect.Enabled = true;
                            clientSocket.Close();
                        }
                        else
                        {
                            //if connected properly, buttons for disconnection, browsing file and uploading will be enabled.
                            connected = true;
                            buttonDisconnect.Enabled = true;
                            buttonBrowse.Enabled = true;
                            ButtonUpload.Enabled = true;
                            string msg = "User " + textBoxUsername.Text + " has connected to the server...\n";
                            Logs.AppendText(msg);
                        }
                    }
                    catch
                    {
                        Logs.AppendText("Problem Occured While Connecting...\n");
                    }
                }
                else
                {
                    Logs.AppendText("Problem Occured While Connecting...\n");
                }
            }
            else
            {
                Logs.AppendText("Username cannot be \"Disconnect\", \"Done\", or empty string.\n");
            }
        }


        private void button1_Click_1(object sender, EventArgs e)
        {
            //Browse Button, use a file dialog to select txt file to upload
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = @"C:\",
                Title = "Browse Text Files",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "txt",
                Filter = "txt files (*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                //write the file name to the text box assigned to file.
                textBoxFile.Text = openFileDialog.FileName;
            }
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {//Disconnecting from server
            if (connected)
            {
                //tell the server that you have disconnected.
                string disconnect = "Disconnect";
                Byte[] bufferDisconnect = new byte[64];
                bufferDisconnect = Encoding.Default.GetBytes(disconnect);
                try
                {
                    clientSocket.Send(bufferDisconnect);
                    clientSocket.Close();
                }
                catch { Console.WriteLine("Error"); }//If the server is closed before client has disconnected, send and close will not work
                buttonConnect.Enabled = true;
                buttonDisconnect.Enabled = false;
                buttonBrowse.Enabled = false;
                ButtonUpload.Enabled = false;
                textBoxFile.Text = "";
                connected = false;
                Logs.AppendText("Disconnected from server...\n");
            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (connected)
            {
                //if the window is closed, tell the server that you disconnect before closing the window
                string disconnect = "Disconnect";
                Byte[] bufferDisconnect = new byte[64];
                bufferDisconnect = Encoding.Default.GetBytes(disconnect);
                try
                {
                    clientSocket.Send(bufferDisconnect);
                    clientSocket.Close();
                }
                catch { }//If the server is closed before client has disconnected, send and close will not work
                connected = false;
                terminating = true;
            }
            Environment.Exit(0);
        }

        private void ButtonUpload_Click(object sender, EventArgs e)
        {
            
            if (connected)
            {
                if (textBoxFile.Text != "")
                {
                    //splitting the directory string to folders and txt file
                    string[] directories = textBoxFile.Text.Split('\\');
                    //the file we want to send is at the end of the directory string
                    string fileName = directories[directories.Length - 1];

                    if (fileName.Length <= 64)
                    {
                        try
                        {
                            string instruction = "UploadFile";
                            Byte[] instructionBuffer = new byte[64];
                            instructionBuffer = Encoding.Default.GetBytes(instruction);
                            clientSocket.Send(instructionBuffer);
                            Thread.Sleep(200);

                            
                            //first, send the file name to the server
                            Byte[] bufferFileName = new byte[64];
                            bufferFileName = Encoding.Default.GetBytes(fileName);
                            clientSocket.Send(bufferFileName);

                            string msg = "Sending the file " + fileName + "\n";
                            Logs.AppendText(msg);
                            Thread.Sleep(200);

                            //tell the server that we are done with sending file name
                            string done = "Done";
                            Byte[] bufferDone = new byte[64];
                            bufferDone = Encoding.Default.GetBytes(done);
                            clientSocket.Send(bufferDone);

                            //read the file byte by byte and send it to the server
                            Stream fileStream = File.OpenRead(textBoxFile.Text);
                            Byte[] fileBuffer = new byte[fileStream.Length];
                            fileStream.Read(fileBuffer, 0, (int)fileStream.Length);
                            //send the files in packets of 1 MB
                            Byte[] sendBuffer = new byte[1048576];
                            int pos = 0;
                            long remainingFileLength = fileStream.Length;
                            while (remainingFileLength >= 1048576)
                            {//If the file is larger than 1MB, we will send multiple 1MB files to send the whole file
                                //transfer file data from file buffer to the buffer that we will send to the server
                                for (int i = 0; i < 1048576; i++)
                                {
                                    sendBuffer[i] = fileBuffer[pos];
                                    pos++;
                                }
                                
                                clientSocket.Send(sendBuffer);
                                //Thread.Sleep(200);
                                
                                //confirm that the packet is received by the server
                                Byte[] recBuffer = new byte[5];
                                clientSocket.Receive(recBuffer);
                                
                                //tell the server that the 1MB packet is done sending
                                clientSocket.Send(bufferDone);
                                remainingFileLength -= 1048576;
                            }
                            if (remainingFileLength > 0)
                            {
                                //either the file is less than 1MB or after multiple 1MB packets, if there is remaining data to be sent that is less than 1MB

                                //transfer file data from file buffer to the buffer that we will send to the server
                                for (int i = 0; i < remainingFileLength; i++)
                                {
                                    sendBuffer[i] = fileBuffer[pos];
                                    pos++;
                                }
                                for (int i = (int)remainingFileLength; i < 1048576; i++)
                                {
                                    sendBuffer[i] = Encoding.Default.GetBytes("\0")[0];
                                }
                                clientSocket.Send(sendBuffer);
                                //Thread.Sleep(200);

                                //confirm that the packet is received by the server
                                Byte[] recBuffer = new byte[5];
                                clientSocket.Receive(recBuffer);
                                //tell the server that we are done sending the packet
                                clientSocket.Send(bufferDone);
                            }

                            //tell the server that the entire file is uploaded
                            string fileDone = "FileDone";
                            Byte[] fileDoneBuffer = new byte[64];
                            fileDoneBuffer = Encoding.Default.GetBytes(fileDone);
                            Thread.Sleep(200);
                            clientSocket.Send(fileDoneBuffer);
                            Logs.AppendText("File " + fileName + " has been sent.\n");
                            Thread.Sleep(200);
                            clientSocket.Send(bufferDone);
                        }
                        catch
                        {//If the server or uploading process crashes at any moment during upload, we will stop uploading the file and disconnect from the server
                            buttonConnect.Enabled = true;
                            buttonDisconnect.Enabled = false;
                            buttonBrowse.Enabled = false;
                            ButtonUpload.Enabled = false;
                            textBoxFile.Text = "";
                            connected = false;
                            try
                            {
                                clientSocket.Close();
                            }
                            catch { }
                            Logs.AppendText("Disconnected from server... Upload interrupted.\n");
                        }
                    }
                    else
                    {
                        Logs.AppendText("File Name cannot have more than 64 characters.\n");
                    }
                }
                else
                {
                    Logs.AppendText("No file selected. Please browse the file you want to upload.\n");
                }
            }
            else
            {
                Logs.AppendText("Cannot upload file because you are not connected to the server\n");
            }
        }

        private void FileListButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                try
                {
                    string instruction = "GetFileList";
                    Byte[] instructionBuffer = new byte[64];
                    instructionBuffer = Encoding.Default.GetBytes(instruction);
                    clientSocket.Send(instructionBuffer);
                    Logs.AppendText("Requested owned files from the server\n");
                    /*
                                        Byte[] recBuffer = new byte[256];
                                        clientSocket.Receive(recBuffer);
                                        string fileInfo = Encoding.Default.GetString(recBuffer);
                                        fileInfo = fileInfo.Substring(0, fileInfo.IndexOf("\0"));
                                        Console.WriteLine(fileInfo + "\n");*/
                    string fileInfo = "";
                    int count = 1;

                    while (fileInfo != "Done" && fileInfo != "Disconnect")
                    {
                        Byte[] recBuffer = new Byte[256];
                        clientSocket.Receive(recBuffer);
                        fileInfo = Encoding.Default.GetString(recBuffer);
                        fileInfo = fileInfo.Substring(0, fileInfo.IndexOf("\0"));

                        if(fileInfo != "Done")
                        {
                            Logs.AppendText(count.ToString() + ") " + fileInfo + "\n");
                            count++;
                        }
                        

                        string done_msg = "Done";
                        Byte[] done_ack = new Byte[4];
                        done_ack = Encoding.Default.GetBytes(done_msg);
                        clientSocket.Send(done_ack);
                    }
                    if (fileInfo == "Done")
                    {
                        Logs.AppendText("All of your files are listed above...\n");

                        Byte[] BuffDone = new Byte[4];
                        string msg = "Done";
                        BuffDone = Encoding.Default.GetBytes(msg);
                        clientSocket.Send(BuffDone);
                    }
                    else if (fileInfo == "Disconnect")
                    {
                        Logs.AppendText("Disconnected from server...\n");

                        Byte[] BuffDone = new Byte[16];
                        string msg = "Disconnect";
                        BuffDone = Encoding.Default.GetBytes(msg);
                        clientSocket.Send(BuffDone);
                        connected = false;
                    }
                }
                catch
                {
                    buttonConnect.Enabled = true;
                    buttonDisconnect.Enabled = false;
                    buttonBrowse.Enabled = false;
                    ButtonUpload.Enabled = false;
                    textBoxFile.Text = "";
                    connected = false;
                    try
                    {
                        clientSocket.Close();
                    }
                    catch { }
                    Logs.AppendText("Disconnected from server... File listing interrupted.\n");
                }
            }
            else
                Logs.AppendText("Cannot request file list because you are not connected to the server\n");
        }

        private void PublicFileListButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                string instruction = "GetPublicFileList";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Logs.AppendText("Requested public files from the server\n");

                
                string fileInfo = "";
                int count = 1;

                while (fileInfo != "Done" && fileInfo != "Disconnect")
                {
                    Byte[] recBuffer = new byte[256];
                    clientSocket.Receive(recBuffer);
                    fileInfo = Encoding.Default.GetString(recBuffer);
                    fileInfo = fileInfo.Substring(0, fileInfo.IndexOf("\0"));

                    if(fileInfo != "Done")
                    {
                        Logs.AppendText(count.ToString() + ") " + fileInfo + "\n");
                        count++;
                    }
                    

                    string done_msg = "Done";
                    Byte[] done_ack = new Byte[4];
                    done_ack = Encoding.Default.GetBytes(done_msg);
                    clientSocket.Send(done_ack);
                }
                if (fileInfo == "Done")
                {
                    Logs.AppendText("All of public files are listed above...\n");

                    Byte[] BuffDone = new Byte[4];
                    string msg = "Done";
                    BuffDone = Encoding.Default.GetBytes(msg);
                    clientSocket.Send(BuffDone);
                }
                else if (fileInfo == "Disconnect")
                {
                    Logs.AppendText("Disconnected from server...\n");
                    Byte[] BuffDone = new Byte[16];
                    string msg = "Disconnect";
                    BuffDone = Encoding.Default.GetBytes(msg);
                    clientSocket.Send(BuffDone);
                    connected = false;
                }
            }
            else
                Logs.AppendText("Cannot request public file list because you are not connected to the server\n");
        }







        private void buttonDownload_Click(object sender, EventArgs e)
        {//Download Private
            if (connected)
            {
                int same_filename_count = 0;
                string instruction = "DownloadPrivate";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Thread.Sleep(200);
                
                string fileName = textBoxFileName.Text;
                Byte[] fileNameBuffer = new byte[64];
                fileNameBuffer = Encoding.Default.GetBytes(fileName);
                clientSocket.Send(fileNameBuffer);
                Thread.Sleep(200);

                Byte[] buffDone = new byte[128];
                string done_msg;
                clientSocket.Receive(buffDone);
                done_msg = Encoding.Default.GetString(buffDone);

                if(done_msg.IndexOf('\0') != -1)
                {
                    done_msg = done_msg.Substring(0, done_msg.IndexOf('\0'));
                }



                
                //Bu mesajları da dosyanın içeriğini aldığım aynı buffer'a alıyom. Ama serverda gönderilen buffer 128 bytelık, sıkıntı olur mu
                if(done_msg == "Done")
                {
                    string logmsg = "Started downloading " + fileName + " from your private folder\n";
                    Logs.AppendText(logmsg);
                    string dir = textBoxDir.Text;
                    System.IO.FileStream Fs = null;

                    // CREATE A FILE WITH SEND FILENAME TO THE SELECTED DIRECTORY
                    if (!File.Exists(dir + "\\" + fileName))
                    {
                        Console.WriteLine(dir + "\\" + fileName);
                        Fs = new System.IO.FileStream(dir + "\\" + fileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                    }

                    else
                    {
                        same_filename_count = get_file_count(dir + "\\" + fileName);
                        if (fileName.IndexOf('(') == -1)
                        {
                            Fs = new System.IO.FileStream(dir + "\\" + fileName.Substring(0, fileName.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                            fileName = fileName.Substring(0, fileName.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt";
                        }
                        else
                        {
                            Fs = new System.IO.FileStream(dir + "\\" + fileName.Substring(0, fileName.IndexOf("(")) + "(" + same_filename_count.ToString() + ").txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                            fileName = fileName.Substring(0, fileName.IndexOf("(")) + "(" + same_filename_count.ToString() + ").txt";
                        }
                    }

                    string file_path = dir + "\\" + fileName;
                    bool reading = true;

                    while (connected && reading)
                    {
                        Byte[] content_buff = new Byte[1048576];
                        clientSocket.Receive(content_buff);
                        string content = Encoding.Default.GetString(content_buff);

                        if(content.IndexOf('\0') != -1)
                        {
                            content = content.Substring(0, content.IndexOf('\0'));
                        }
                        

                        if(content.IndexOf("succesfully downloaded from your private folder.") == -1)
                        {
                            Fs.Close();
                            using (StreamWriter sw = File.AppendText(file_path))
                            {
                                sw.Write(content);
                            }
                        }

                        else
                        {
                            Logs.AppendText(content + "\n");
                            reading = false;
                        }

                        Byte[] done_ack = new byte[4];
                        string message = "Done";
                        done_ack = Encoding.Default.GetBytes(message);
                        clientSocket.Send(done_ack);
                    }
                }

                else
                {
                    Logs.AppendText(done_msg + "\n");
                    
                    Byte[] done_ack = new byte[4];
                    string msg = "Done";
                    done_ack = Encoding.Default.GetBytes(msg);
                    clientSocket.Send(done_ack);
                }
                
            }
            else
                Logs.AppendText("Cannot download file because you are not connected to the server\n");
        }








        private void buttonCopy_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                string instruction = "CopyFile";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Thread.Sleep(200);

                string fileName = textBoxFileName.Text;
                Byte[] fileNameBuffer = new byte[64];
                fileNameBuffer = Encoding.Default.GetBytes(fileName);
                clientSocket.Send(fileNameBuffer);

                Byte[] recBuffer = new byte[128];
                clientSocket.Receive(recBuffer);
                string recMessage = Encoding.Default.GetString(recBuffer);
                recMessage = recMessage.Substring(0, recMessage.IndexOf("\0"));
                Logs.AppendText(recMessage + "\n");


                Byte[] doneBuff = new byte[4];
                string msg = "Done";
                doneBuff = Encoding.Default.GetBytes(msg);
                clientSocket.Send(doneBuff);
            }
            else
                Logs.AppendText("Cannot copy file because you are not connected to the server\n");
        }








        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                string instruction = "DeleteFile";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Thread.Sleep(200);

                string fileName = textBoxFileName.Text;
                Byte[] fileNameBuffer = new byte[64];
                fileNameBuffer = Encoding.Default.GetBytes(fileName);
                clientSocket.Send(fileNameBuffer);

                Byte[] recBuffer = new byte[64];
                clientSocket.Receive(recBuffer);
                string recMessage = Encoding.Default.GetString(recBuffer);
                recMessage = recMessage.Substring(0, recMessage.IndexOf("\0"));
                Logs.AppendText(recMessage + "\n");


                Byte[] doneBuff = new byte[4];
                string msg = "Done";
                doneBuff = Encoding.Default.GetBytes(msg);
                clientSocket.Send(doneBuff);
            }
            else
                Logs.AppendText("Cannot delete file because you are not connected to the server\n");
        }








        private void buttonPublish_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                string instruction = "MakePublic";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Thread.Sleep(200);
                string fileName = textBoxFileName.Text;
                Byte[] fileNameBuffer = new byte[64];
                fileNameBuffer = Encoding.Default.GetBytes(fileName);
                clientSocket.Send(fileNameBuffer);

                Byte[] recBuffer = new byte[64];
                clientSocket.Receive(recBuffer);
                string recMessage = Encoding.Default.GetString(recBuffer);
                recMessage = recMessage.Substring(0, recMessage.IndexOf("\0"));
                Logs.AppendText(recMessage + "\n");


                Byte[] done_buff = new byte[4];
                string msg = "Done";
                done_buff = Encoding.Default.GetBytes(msg);
                clientSocket.Send(done_buff);
            }
            else
                Logs.AppendText("Cannot publish file because you are not connected to the server\n");
        }









        private void buttonDownloadPublic_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                int same_filename_count = 0;
                string instruction = "DownloadPublic";
                Byte[] instructionBuffer = new byte[64];
                instructionBuffer = Encoding.Default.GetBytes(instruction);
                clientSocket.Send(instructionBuffer);
                Thread.Sleep(200);

                string owner = textBoxOwner.Text;
                Byte[] ownerBuffer = new byte[64];
                ownerBuffer = Encoding.Default.GetBytes(owner);
                clientSocket.Send(ownerBuffer);
                Thread.Sleep(200);

                string fileName = textBoxFileName.Text;
                Byte[] fileNameBuffer = new byte[64];
                fileNameBuffer = Encoding.Default.GetBytes(fileName);
                clientSocket.Send(fileNameBuffer);

                Byte[] buffDone = new byte[128];
                string done_msg;
                clientSocket.Receive(buffDone);
                done_msg = Encoding.Default.GetString(buffDone);

                if(done_msg.IndexOf('\0') != -1)
                {
                    done_msg.Substring(0, done_msg.IndexOf('\0'));
                }

                
                

                //Bu mesajları da dosyanın içeriğini aldığım aynı buffer'a alıyom. Ama serverda gönderilen buffer 128 bytelık, sıkıntı olur mu
                if (done_msg.IndexOf("Done") != -1)
                {
                    Console.WriteLine("Inside If");
                    string logmsg = "Started downloading " + fileName + " from the public folder\n";
                    Logs.AppendText(logmsg);
                    string dir = textBoxDir.Text;
                    System.IO.FileStream Fs = null;

                    // CREATE A FILE WITH SEND FILENAME TO THE SELECTED DIRECTORY
                    if (!File.Exists(dir + "\\" + fileName))
                    {
                        Console.WriteLine(dir + "\\" + fileName);
                        Fs = new System.IO.FileStream(dir + "\\" + fileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                    }

                    else
                    {
                        same_filename_count = get_file_count(dir + "\\" + fileName);
                        if(fileName.IndexOf('(') == -1)
                        {
                            Fs = new System.IO.FileStream(dir + "\\" + fileName.Substring(0, fileName.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                            fileName = fileName.Substring(0, fileName.IndexOf(".txt")) + "(" + same_filename_count.ToString() + ").txt";
                        }
                        else
                        {
                            Fs = new System.IO.FileStream(dir + "\\" + fileName.Substring(0, fileName.IndexOf("(")) + "(" + same_filename_count.ToString() + ").txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                            fileName = fileName.Substring(0, fileName.IndexOf("(")) + "(" + same_filename_count.ToString() + ").txt";
                        }
                    }

                    string file_path = dir + "\\" + fileName;
                    bool reading = true;

                    while (connected && reading)
                    {
                        Byte[] content_buff = new Byte[1048576];
                        clientSocket.Receive(content_buff);
                        string content = Encoding.Default.GetString(content_buff);

                        if (content.IndexOf('\0') != -1)
                        {
                            content = content.Substring(0, content.IndexOf('\0'));
                        }

                        if (content.IndexOf("succesfully downloaded from the public folder.") == -1)
                        {
                            Fs.Close();
                            using (StreamWriter sw = File.AppendText(file_path))
                            {
                                sw.Write(content);
                            }
                        }

                        else
                        {
                            Logs.AppendText(content + "\n");
                            reading = false;
                        }


                        Byte[] done_ack = new byte[4];
                        string message = "Done";
                        done_ack = Encoding.Default.GetBytes(message);
                        clientSocket.Send(done_ack);
                    }
                }

                else
                {
                    Logs.AppendText(done_msg + "\n");

                    Byte[] done_ack = new byte[4];
                    string msg = "Done";
                    done_ack = Encoding.Default.GetBytes(msg);
                    clientSocket.Send(done_ack);
                }

            }
            else
                Logs.AppendText("Cannot download file because you are not connected to the server\n");
        }








        private void buttonBrowseDir_Click(object sender, EventArgs e)
        {
            string SaveFileName = string.Empty;

            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            folderBrowserDialog1.ShowDialog();

            string path = folderBrowserDialog1.SelectedPath;

            textBoxDir.Text = path;
            Logs.AppendText("Selected download path: " + path + "\n");
        }






        private void label1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
