using System;

namespace Lab5
{
    class Program
    {
        static void Main(string[] args)
        {
            FTPClient ftp = new FTPClient();

            if(ftp.Start("localhost", 21)){
                ftp.Login("Ano", "1234");
                ftp.Working();
                // Console.ReadKey();
            }
            ftp.Stop();
        }
    }
}
