using System.Net;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System;
using System.Net.Sockets;

namespace Lab5{
    class FTPClient{

        enum LoginState{
            User,
            Pass,
            Success
        }
        enum ConnectionMode{
            Active,
            Passive
        }
        private Socket socket;
        private NetworkStream cmdStream;
        private Socket dataSocket;
        private NetworkStream dataStream;
        private LoginState loginState;
        private ConnectionMode connectionMode;
        private string activeAddress;

        private string username;

        private bool work;

        private string downloadFolder;

        public FTPClient(){}

        public bool Start(string hostname, int port){
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try{
                socket.Connect(hostname, port);
                cmdStream = new NetworkStream(socket);
            }catch(Exception ex){
                Console.WriteLine(ex);
                Console.WriteLine("Cannot connect to the server!");
                return false;
            }

            downloadFolder = "./Downloads";
            Directory.CreateDirectory(downloadFolder);

            Console.WriteLine(Read());

            loginState = LoginState.User;
            connectionMode = ConnectionMode.Passive;

            return true;
        }

        public void Login(string username, string password){
            switch(loginState){
                case LoginState.User:
                    Send($"USER {username}");
                    loginState = LoginState.Pass;
                break;
                case LoginState.Pass:
                    Send($"PASS {password}");
                    work = true;
                    this.username = username;
                    Console.WriteLine("You successfuly authorizated!");
                    loginState = LoginState.Success;
                break;
            }

            string line = Read();
            Console.WriteLine(line);

            if((line.StartsWith('1') || line.StartsWith('2') || line.StartsWith('3')) && loginState != LoginState.Success){
                Login(username, password);
            }else{
                return;
            }
        }

        public void Working(){
            if(loginState == LoginState.Success){
                while(work){
                    Console.Write(">");
                    string cmd = Console.ReadLine();
                    string[] cmdParts = cmd.Split(' ');
                    if(cmdParts.Length > 0){
                        cmdParts[0] = cmdParts[0].ToLowerInvariant();
                        switch(cmdParts[0]){
                            case "cd":
                                ChangeDirectory(cmdParts[1]);
                            break;
                            case "ls":
                                switch(connectionMode){
                                    case ConnectionMode.Active:
                                        ActiveMode(activeAddress);
                                    break;
                                    case ConnectionMode.Passive:
                                        PassiveMode();
                                    break;
                                }
                                ShowDirectory();
                            break;
                            case "pwd":
                                ShowWorkingDirectory();
                            break;
                            case "wget":
                                switch(connectionMode){
                                    case ConnectionMode.Active:
                                        ActiveMode(activeAddress);
                                    break;
                                    case ConnectionMode.Passive:
                                        PassiveMode();
                                    break;
                                }
                                Download(cmdParts[1]);
                            break;
                            case "store":
                                switch(connectionMode){
                                    case ConnectionMode.Active:
                                        ActiveMode(activeAddress);
                                    break;
                                    case ConnectionMode.Passive:
                                        PassiveMode();
                                    break;
                                }
                                StoreFile(cmdParts[1]);
                            break;
                            case "exit":
                                Quit();
                                work = false;
                            break;
                            case "clear":
                                Console.Clear();
                            break;
                            case "reset":
                                Reinitialization();
                            break;
                            case "setmode":
                                SetConnectionMode(cmdParts[1]);
                            break;
                            case "rename":
                                if(cmdParts.Length > 2){
                                    RenameFile($"{cmdParts[1]}/{cmdParts[2]}");
                                }
                            break;
                            default:
                                Console.WriteLine("I don't know that command!");
                            break;
                        }
                    }else{
                        Console.WriteLine("Command invalide!");
                    }
                }
            }
        }

        private void CustomCmd(string cmd){
            Send(cmd);
            Console.WriteLine(Read());
        }

        private void ChangeDirectory(string args){
            Send($"CWD {args}");
            Console.WriteLine(Read());
        }

        private void ShowWorkingDirectory(){
            Send($"PWD");
            Console.WriteLine(Read());
        }

        private void Quit(){
            Send($"QUIT");
            Console.WriteLine(Read());
        }

        private void Reinitialization(){
            Send($"REIN");
            Console.WriteLine(Read());
        }

        private void Abort(){
            Send($"ABOR");
            Console.WriteLine(Read());
        }

        private void SetConnectionMode(string args){
            string[] parms = args.Split(' '); 
            switch(parms[0]){
                case "active":
                    if(parms.Length > 1){
                        activeAddress = parms[1];
                        connectionMode = ConnectionMode.Active;
                    }else{
                        Console.WriteLine("Too few arguments");
                    }
                break;
                case "passive":
                    connectionMode = ConnectionMode.Passive;
                break;
            }
            Console.WriteLine($"Connection setted up to {parms[0]} mode");
        }

        private void ActiveMode(string args){
            Send($"PORT {args}");
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string[] address = args.Split(',');
            byte[] addressBytes = new byte[4];
            byte[] portBytes = new byte[2];
            addressBytes[0] = byte.Parse(address[0]);
            addressBytes[1] = byte.Parse(address[1]);
            addressBytes[2] = byte.Parse(address[2]);
            addressBytes[3] = byte.Parse(address[3]);
            portBytes[0] = byte.Parse(address[4]);
            portBytes[1] = byte.Parse(address[5]);
            IPAddress ip = new IPAddress(addressBytes);
            int port = (int)(portBytes[0] * 256 + portBytes[1]);
            IPEndPoint endPoint = new IPEndPoint(ip, port);
            listenSocket.Bind(endPoint);
            listenSocket.Listen(1);
            dataSocket = listenSocket.Accept();
            dataStream = new NetworkStream(dataSocket);
            Console.WriteLine(Read());
        }

        private void PassiveMode(){
            Send($"PASV");
            dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string line = Read();
            string address = line.Substring(27);
            address = address.Replace("(", "");
            address = address.Replace(")", "");
            string[] octets = address.Split(',');
            byte[] addressBytes = new byte[4];
            byte[] portBytes = new byte[2];
            addressBytes[0] = byte.Parse(octets[0]);
            addressBytes[1] = byte.Parse(octets[1]);
            addressBytes[2] = byte.Parse(octets[2]);
            addressBytes[3] = byte.Parse(octets[3]);
            portBytes[0] = byte.Parse(octets[4]);
            portBytes[1] = byte.Parse(octets[5]);
            IPAddress ip = new IPAddress(addressBytes);
            int port = (int)(portBytes[0] * 256 + portBytes[1]);
            while(!dataSocket.Connected){
                try{
                    dataSocket.Connect(ip, port);
                }catch(Exception ex){}
            }
            dataStream = new NetworkStream(dataSocket);
            Console.WriteLine(line);
        }

        private void SetType(string args){
            Send($"TYPE {args}");
            Console.WriteLine(Read());
        }

        private void SetStruct(string args){
            Send($"STRU {args}");
            Console.WriteLine(Read());
        }

        private void SetMode(string args){
            Send($"MODE {args}");
            Console.WriteLine(Read());
        }

        private void DeleteFile(string args){
            Send($"DELE {args}");
            Console.WriteLine(Read());
        }

        private void CreateDirectory(string args){
            Send($"MKD {args}");
            Console.WriteLine(Read());
        }

        private void DeleteDirectory(string args){
            Send($"RMD {args}");
            Console.WriteLine(Read());
        }

        private void Download(string args){
            Send($"RETR {args}");
            Console.WriteLine(Read());
            List<byte> data = new List<byte>();
            byte[] buffer = new byte[1024];
            Array.Clear(buffer, 0, buffer.Length);
            try{
                do{
                    dataStream.Read(buffer, 0, buffer.Length);
                    data.AddRange(buffer);
                    Array.Clear(buffer, 0, buffer.Length);
                }while(dataStream.DataAvailable);
            }catch(Exception ex){
                Console.WriteLine(ex);
            }
            File.WriteAllBytes($"{downloadFolder}/{args}", data.ToArray());
            dataStream.Close();
            dataSocket.Close();
            string line = Read();
            if(line.StartsWith('5'))
                File.Delete(args);
            Console.WriteLine(line);
        }

        private void ShowDirectory(){
            Send($"LIST");
            Console.WriteLine(Read());
            List<byte> data = new List<byte>();
            byte[] buffer = new byte[1024];
            Array.Clear(buffer, 0, buffer.Length);
            try{
                do{
                    dataStream.Read(buffer, 0, buffer.Length);
                    data.AddRange(buffer);
                    Array.Clear(buffer, 0, buffer.Length);
                }while(dataStream.DataAvailable);
            }catch(Exception ex){
                Console.WriteLine(ex);
            }
            dataStream.Close();
            dataSocket.Close();
            Console.WriteLine(Read());
            Console.WriteLine(Encoding.UTF8.GetString(data.ToArray()));

        }

        private void StoreFile(string args){
            int separator = 0;
            if(args.Contains('/'))
                separator = args.LastIndexOf('/') + 1;
            else if(args.Contains('\\'))
                separator = args.LastIndexOf('\\') + 1;
            string fileName = args.Substring(separator);
            byte[] buffer = File.ReadAllBytes(args);
            Send($"STOR {fileName}");
            Console.WriteLine(Read());
            try{
                dataStream.Write(buffer);
                dataStream.Flush();
            }catch(Exception ex){
                Console.WriteLine(ex);
            }
            dataStream.Close();
            dataSocket.Close();
            Console.WriteLine(Read());
        }

        private void RenameFile(string args){
            string[] names = args.Split('/');
            Send($"RNFR {names[0]}");
            Console.WriteLine(Read());
            Send($"RNTO {names[1]}");
            Console.WriteLine(Read());
        }

        public bool Stop(){
            if(socket.Connected){
                socket.Disconnect(false);
                socket = null;
                return true;
            }
            return false;
        }

        public void Send(string command){
            if(socket.Connected){
                command = command + "\r\n";
                byte[] message = Encoding.UTF8.GetBytes(command);
                cmdStream.Write(message, 0, message.Length);
                cmdStream.Flush();
            }
        }

        public string Read(){
            if(socket.Connected){
                List<byte> msg = new List<byte>();
                byte[] buffer = new byte[1024];
                try{
                    do{
                        cmdStream.Read(buffer, 0, buffer.Length);
                        msg.AddRange(buffer);
                    }while(cmdStream.DataAvailable);
                }catch(Exception ex){}
                return Encoding.UTF8.GetString(msg.ToArray());
            }
            return "Cant read!";
        }

        private string TranslateMessage(byte[] message){
            return Encoding.UTF8.GetString(message);
        }
    }
}