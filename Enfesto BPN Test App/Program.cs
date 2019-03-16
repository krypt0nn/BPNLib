using BPN;
using System;

namespace Enfesto_BPN_Test_App
{
    class Program
    {
        static void Main(string[] args)
        {
            BPNClient client = new BPNClient ("test");

            /*BPNTunnel tunnel = new BPNTunnel ();
            tunnel.SetName ("sasaki ken");
            tunnel.SetBridge ("192.168.100.7");*/

            Console.WriteLine ("Actions:\n\n0 - recieve data\n1 - send data\n\n");

            switch (Console.ReadLine ())
            {
                case "0":
                    client.SetDataRecieveHandler ((data) => {
                        Console.WriteLine ("Recieved from '" + data["name"] + "': " + data["data"]);
                    });

                    client.Listen (true);

                    break;

                case "1":
                    Console.Write ("Enter client IP: ");
                    string ip = Console.ReadLine ();

                    Console.WriteLine ("Enter data to send: ");
                    
                    client.Push (ip, Console.ReadLine ());

                    //tunnel.Push (ip, Console.ReadLine ());

                    Console.WriteLine ("\nData succesfuly sended");
                    //Console.WriteLine ("\nData sended, state: " + tunnel.Send (ip, Console.ReadLine ()));

                    break;
            }

            Console.ReadKey ();
        }
    }
}
