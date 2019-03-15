using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BPN
{
    public class BPNClient
    {
        public const int headerSize     = 80;
        public const int checksumPos    = 0;
        public const int checksumSize   = 16;
        public const int IPAddressPos   = 16;
        public const int IPAddressSize  = 15;
        public const int senderNamePos  = 31;
        public const int senderNameSize = 33;
        public const int flagPos        = 64;
        public const int flagSize       = 16;

        public int RecievePacketsSize { set; get; } = 2048;
        public int ConnectionDelay { set; get; }    = 2000;
        public int TunnelingRepeats { get; set; }   = 10;
        public string Name { get; }

        protected string flag = "";
        
        protected int port;
        protected TcpListener listener;
        
        protected List<Dictionary<string, string>> data = new List<Dictionary<string, string>> ();
        protected Action<Dictionary<string, string>> dataRecieveHandler;

        public BPNClient ()
        {
            this.Name = Environment.UserName;
            this.port = 42569;

            if (this.Name.Length > senderNameSize)
                this.Name = this.Name.Substring (0, senderNameSize);
        }

        public BPNClient (string name, int port = 42569)
        {
            if (name.Length > senderNameSize)
                throw new Exception ("Client name mustn't be longer than " + senderNameSize + " chars");

            this.Name = name;
            this.port = port;
        }

        public void Listen (bool infinity = false)
        {
            this.listener = new TcpListener (IPAddress.Any, this.port);
            this.listener.Start ();

            while (true)
                if (this.listener.Pending ())
                {
                    new Thread (new ParameterizedThreadStart (this.CreateSession)).Start (this.listener.AcceptTcpClient ());

                    if (!infinity)
                        break;
                }
        }
        
        public bool Send (string ip, string data)
        {
            return this.Send (ip, this.port, data);
        }
        
        public bool Send (string ip, int port, string data)
        {
            IPAddress address;

            if (!IPAddress.TryParse (ip, out address))
                throw new Exception ("ip parameter must be right ip address");

            TcpClient client = new TcpClient ();
            client.ConnectAsync (ip, port).Wait (ConnectionDelay);

            if (!client.Connected)
                return false;

            else
            {
                data = Convert.ToBase64String (Encoding.ASCII.GetBytes (data));

                string header = "";
                string selfIp = "";

                for (int i = 0; i < headerSize; ++i)
                    header += '\0';

                try
                {
                    selfIp = new WebClient ().DownloadString ("https://api.ipify.org");
                }

                catch (Exception) { }

                byte[] buffer = Encoding.ASCII.GetBytes (
                    header
                        .Insert (checksumPos, this.GetChecksum (data))
                        .Insert (IPAddressPos, selfIp)
                        .Insert (senderNamePos, Name)
                        .Insert (flagPos, this.flag)
                        .Insert (headerSize, data)
                );

                client.GetStream ().Write (buffer, 0, buffer.Length);

                return true;
            }
        }

        public void Push (string ip, string data)
        {
            while (!this.Send (ip, data));
        }

        public void Push (string ip, int port, string data)
        {
            while (!this.Send (ip, port, data));
        }

        public List<Dictionary<string, string>> Get ()
        {
            return this.data;
        }

        public List<Dictionary<string, string>> Get (string name)
        {
            return this.GetBy ("name", name);
        }

        public List<Dictionary<string, string>> GetBy (string name, string value)
        {
            List<Dictionary<string, string>> items = new List<Dictionary<string, string>> ();

            foreach (Dictionary<string, string> item in data)
                if (item[name] == value)
                    items.Add (item);

            return items;
        }

        public void SetDataRecieveHandler (Action<Dictionary<string, string>> handler)
        {
            this.dataRecieveHandler = handler;
        }

        public void SetFlag (string flag)
        {
            if (flag.Length > flagSize)
                throw new Exception ("Flag mustn't be longer than " + flagSize + " chars");

            else this.flag = flag;
        }

        protected string GetChecksum (string data)
        {
            return Convert.ToBase64String (new SHA256Cng ().ComputeHash (Encoding.ASCII.GetBytes (data))).Substring (0, checksumSize);
        }

        protected bool VerifyChecksum (string data, string checksum)
        {
            return this.GetChecksum (data) == checksum;
        }

        protected void CreateSession (object threadParam)
        {
            TcpClient client = (TcpClient) threadParam;

            while (client.Connected)
                if (client.GetStream ().DataAvailable)
                {
                    string recieved = "";
                    byte[] buffer   = new byte[RecievePacketsSize];

                    while (client.GetStream ().DataAvailable)
                    {
                        int size = client.GetStream ().Read (buffer, 0, RecievePacketsSize);
                        Array.Resize (ref buffer, size);

                        recieved += Encoding.ASCII.GetString (buffer);
                    }

                    List<string> peerData = this.GetPeerData (recieved);
                    recieved = recieved.Substring (headerSize).TrimEnd ();

                    if (this.VerifyChecksum (recieved, peerData[0]))
                    {
                        string data = Encoding.ASCII.GetString (Convert.FromBase64String (recieved));

                        if (peerData[3] == "BPNTunnel")
                        {
                            string[] nodes = data.Substring (0, data.IndexOf (':')).Split (';');
                            data = data.Substring (data.IndexOf (':') + 1);

                            if (nodes.Length > 1)
                            {
                                string ipsLine = string.Join (";", nodes);
                                ipsLine = ipsLine.Substring (ipsLine.IndexOf (';') + 1);

                                BPNClient tunnel = new BPNClient (peerData[2]);
                                tunnel.SetFlag ("BPNTunnel");

                                for (int i = 0; i < TunnelingRepeats; ++i)
                                    if (tunnel.Send (nodes[1], ipsLine + (nodes.Length > 0 ? ":" : "") + data))
                                        break;

                                return;
                            }
                        }
                        
                        this.data.Add (new Dictionary<string, string> {
                            {"ip",   peerData[1]},
                            {"name", peerData[2]},
                            {"flag", peerData[3]},
                            {"data", data}
                        });
                        
                        this.dataRecieveHandler (this.data[this.data.Count - 1]);
                    }
                }

            client.Close ();
        }

        protected List<string> GetPeerData (string data)
        {
            data = data.Substring (0, headerSize);

            return new List<string> {
                data.Substring (checksumPos, checksumSize).TrimEnd ('\0'),
                data.Substring (IPAddressPos, IPAddressSize).TrimEnd ('\0'),
                data.Substring (senderNamePos, senderNameSize).TrimEnd ('\0'),
                data.Substring (flagPos, flagSize).TrimEnd ('\0')
            };
        }
    }
    
    public class BPNTunnel
    {
        public string[] Nodes { get; }
        protected string bridge = "";
        protected string name;

        public bool RandomOrder { get; set; } = true;

        public BPNTunnel (params string[] ips)
        {
            this.name  = Environment.UserName;
            this.Nodes = ips;

            if (this.name.Length > BPNClient.senderNameSize)
                this.name = this.name.Substring (0, BPNClient.senderNameSize);
        }

        public void SetName (string name)
        {
            if (name.Length > BPNClient.senderNameSize)
                throw new Exception ("Client name mustn't be longer than " + BPNClient.senderNameSize + " chars");

            else this.name = name;
        }

        public void SetBridge (string ip)
        {
            IPAddress address;

            if (IPAddress.TryParse (ip, out address))
                this.bridge = ip;

            else throw new Exception ("ip parameter must be right ip address");
        }

        public bool Send (string ip, string data)
        {
            return this.Send (ip, 42569, data);
        }

        public bool Send (string ip, int port, string data)
        {
            IPAddress address;

            if (!IPAddress.TryParse (ip, out address))
                throw new Exception ("ip parameter must be right ip address");

            List<string> randomOrderedNodes = new List<string> ();
            int captured = 0;

            if (this.bridge != "")
                randomOrderedNodes.Add (this.bridge);

            if (this.RandomOrder)
                while (captured < this.Nodes.Length)
                    try
                    {
                        int index = new Random ().Next (this.Nodes.Length - 1);

                        if (this.Nodes[index] != "")
                        {
                            randomOrderedNodes.Add (this.Nodes[index]);
                            ++captured;

                            this.Nodes[index] = "";
                        }
                    }

                    catch (Exception)
                    {
                        continue;
                    }

            else randomOrderedNodes.AddRange (this.Nodes);

            randomOrderedNodes.Add (ip);

            BPNClient tunnel = new BPNClient (this.name, port);
            tunnel.SetFlag ("BPNTunnel");

            return tunnel.Send (randomOrderedNodes[0], string.Join (";", randomOrderedNodes) + ':' + data);
        }

        public void Push (string ip, string data)
        {
            while (!this.Send (ip, data));
        }

        public void Push (string ip, int port, string data)
        {
            while (!this.Send (ip, port, data));
        }
    }

    public class BPNPool
    {
        public BPNClient Client { get; }
        public string[] Ips { get; }

        public BPNPool (params string[] ips)
        {
            this.Client = new BPNClient ();
            this.Ips    = ips;
        }

        public BPNPool (string name, params string[] ips)
        {
            this.Client = new BPNClient (name);
            this.Ips    = ips;
        }

        public BPNPool (string name, int port, params string[] ips)
        {
            this.Client = new BPNClient (name, port);
            this.Ips    = ips;
        }

        public bool Send (int poolId, string data)
        {
            if (poolId < 0 || poolId > Ips.Length)
                throw new Exception ("Wrong poolId param");

            return this.Client.Send (this.Ips[poolId], data);
        }

        public void Push (int poolId, string data)
        {
            if (poolId < 0 || poolId > Ips.Length)
                throw new Exception ("Wrong poolId param");

            this.Client.Push (this.Ips[poolId], data);
        }

        public void Broadcast (string data, bool push = false)
        {
            for (int i = 0; i < this.Ips.Length; ++i)
                if (push)
                    this.Push (i, data);

                else this.Send (i, data);
        }
    }
}
