using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Linq;
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
        private Socket socket;
        private NetworkStream msgStream;
        private Socket downloadSocket;
        private NetworkStream fileStream;
        private LoginState loginState;

        private string username;

        private bool work;


        public FTPClient(){}

        public bool Start(string hostname, int port){
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            downloadSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try{
                socket.Connect(hostname, port);
                msgStream = new NetworkStream(socket);
            }catch(Exception ex){
                Console.WriteLine("Cannot connect to the server!");
                return false;
            }

            msgStream.ReadTimeout = 1;

            Console.WriteLine(Read());

            loginState = LoginState.User;

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
                        cmdParts[0] = cmdParts[0].ToUpperInvariant();
                        switch(cmdParts[0]){
                            case "CWD":
                                if(cmdParts.Length > 1)
                                    CWD(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "REIN":
                                REIN();
                            break;
                            case "QUIT":
                                QUIT();
                                work = false;
                            break;
                            case "PORT":
                                if(cmdParts.Length > 1)
                                    PORT(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "PASV":
                                PASV();
                            break;
                            case "TYPE":
                                if(cmdParts.Length > 1)
                                    TYPE(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "STRU":
                                if(cmdParts.Length > 1)
                                    STRU(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "MODE":
                                if(cmdParts.Length > 1)
                                    MODE(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "RETR":
                                if(cmdParts.Length > 1)
                                    RETR(cmdParts[1]);
                                else
                                    Console.WriteLine("Too few arguments");
                            break;
                            case "STOR":
                            break;
                            case "RNAME":
                            break;
                            case "ABOR":
                            break;
                            case "DELE":
                            break;
                            case "MKD":
                            break;
                            case "RMD":
                            break;
                            case "LIST":
                            break;
                            case "CUSTOM":
                                if(cmdParts.Length > 1)
                                    Custom(cmdParts[1]);
                                else
                                    Console.WriteLine("To few arguments");
                            break;
                        }
                    }else{
                        Console.WriteLine("Command invalide!");
                    }
                }
            }
        }

        private void Custom(string cmd){
            Send(cmd);
            Console.WriteLine(Read());
        }

        private void CWD(string args){
            Send($"CWD {args}");
            Console.WriteLine(Read());
        }

        private void QUIT(){
            Send($"QUIT");
            Console.WriteLine(Read());
        }

        private void REIN(){
            Send($"REIN");
            Console.WriteLine(Read());
        }

        private void PORT(string args){
            Send($"PORT {args}");
            Console.WriteLine(Read());
        }

        private void PASV(){
            Send($"PASV");
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
            while(!downloadSocket.Connected){
                try{
                    downloadSocket.Connect(ip, port);
                }catch(Exception ex){}
            }
            fileStream = new NetworkStream(downloadSocket);
            fileStream.ReadTimeout = 1;
            Console.WriteLine(line);
        }

        private void TYPE(string args){
            Send($"TYPE {args}");
            Console.WriteLine(Read());
        }

        private void STRU(string args){
            Send($"STRU {args}");
            Console.WriteLine(Read());
        }

        private void MODE(string args){
            Send($"MODE {args}");
            Console.WriteLine(Read());
        }

        private void RETR(string args){
            Send($"RETR {args}");
            int count = 0;
            List<byte> file = new List<byte>();
            byte[] buffer = new byte[4096];
            try{
                while((count = fileStream.Read(buffer, 0, buffer.Length)) > 0){
                    file.AddRange(buffer);
                    Array.Clear(buffer, 0, buffer.Length);
                }
            }catch(Exception ex){}
            File.WriteAllBytes(args, file.ToArray());
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
                msgStream.Write(message, 0, message.Length);
                msgStream.Flush();
            }
        }

        public string Read(){
            if(socket.Connected){
                int count = 0;
                List<byte> msg = new List<byte>();
                byte[] buffer = new byte[4096];
                try{
                    while((count = msgStream.Read(buffer, 0, buffer.Length)) > 0){
                        msg.AddRange(buffer);
                        Array.Clear(buffer, 0, buffer.Length);
                    }
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