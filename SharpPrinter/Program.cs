using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using NDesk.Options;
using SnmpSharpNet;
using System.Text.RegularExpressions;


namespace SharpPrinter
{
    
    class Program
    {
        
        public class Printers
        {
            public static List<string> PrinterList = new List<string>();
        }
        public class AddressBooks
        {
            public static List<string> AddressList = new List<string>();
        }


        static string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = null;
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        static bool getSnmpnext(string host, string OID)
        {
            bool result = false;

            SimpleSnmp snmpVerb = new SimpleSnmp(host, 161, "public", 500, 0);
            if (!snmpVerb.Valid)
            {
                return result;
            }
            Oid varbind = new Oid(OID);
            Dictionary<Oid, AsnType> snmpDataS = snmpVerb.GetNext(SnmpVersion.Ver1, new string[] { varbind.ToString() });
            if (snmpDataS != null)
            {
                result = true;
            }
            return result;
        }

        static bool getSnmp(string host, string OID, bool adump)
        {
            bool result = false;

            SimpleSnmp snmpVerb = new SimpleSnmp(host, 161, "public", 500, 0);
            if (!snmpVerb.Valid)
            {
                return result;
            }

            Oid varbind = new Oid(OID);

            Dictionary<Oid, AsnType> snmpDataS = snmpVerb.Get(SnmpVersion.Ver1, new string[] { varbind.ToString() });
            if (snmpDataS != null)
            {
                if (adump == true)
                {
                    
                    AddressBooks data = new AddressBooks();
                    string temp = snmpDataS[varbind].ToString();
                    AddressBooks.AddressList.Add(temp);
                }
                else
                {
                    string temp = snmpDataS[varbind].ToString();
                    // Get MANUFACTURER
                    int startIndex = temp.IndexOf("MFG:");
                    int endIndex = temp.IndexOf(";", startIndex);
                    string mfg = temp.Substring(startIndex + 4, endIndex - (startIndex + 4));
                    // Get MODEL
                    startIndex = temp.IndexOf("MDL:");
                    endIndex = temp.IndexOf(";", startIndex);
                    string printerMDL = temp.Substring(startIndex + 4, endIndex - (startIndex + 4));
                    Printers data = new Printers();
                    Printers.PrinterList.Add(host + " " + mfg + " " + printerMDL);
                }

            }

            return result;
        }


        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        static void SendArpRequest(IPAddress dst, bool adump)
        {
            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)macAddr.Length;
            int uintAddress = BitConverter.ToInt32(dst.GetAddressBytes(), 0);

            if (adump == true)
            {
                getSnmp(dst.ToString(), "1.3.6.1.4.1.1347.42.23.1.4.0", true);
            }
            else
            {
                if (SendARP(uintAddress, 0, macAddr, ref macAddrLen) == 0)
                {
                    getSnmpnext(dst.ToString(), "1.3.6.1.2.1.43");
                    if (getSnmpnext(dst.ToString(), "1.3.6.1.2.1.43") == true)
                    {
                        getSnmp(dst.ToString(), "1.3.6.1.4.1.2699.1.2.1.2.1.1.3.1", false);
                    }
                }

            }

        }

        static void ScanPrinters()
        {
            string prefix = null;

            string temp = GetLocalIPv4(NetworkInterfaceType.Ethernet);
            if (temp != null)
                prefix = temp.Substring(0, 3);
            if (temp == null || (prefix == "169"))
                temp = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
            string ipBase = temp;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    ipBase = temp.Remove(ipBase.Length - 1);
                    if (ipBase.EndsWith("."))
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Errormessage = " + ex.Message);
            }

            //Generating IP Range
            List<IPAddress> ipAddressList = new List<IPAddress>();
            for (int i = 1; i < 254; i++)
            {
                //Obviously you'll want to safely parse user input to catch exceptions.
                ipAddressList.Add(IPAddress.Parse(ipBase + i));
            }

            foreach (IPAddress ip in ipAddressList)
            {
                Thread thread = new Thread(() => SendArpRequest(ip, true));
                thread.Start();
            }

        }

        static void Main(string[] args)
        {
            bool AddressDump = false;
            bool showhelp = false;

            var opts = new OptionSet()
            {
                { "AddressDump=", " --AddressDump+", v => AddressDump = v != null },
                { "h|?|help",  "Show available options", v => showhelp = v != null },
            };
            try
            {
                opts.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
            }
            if (showhelp)
            {
                Console.WriteLine("RTFM");
                opts.WriteOptionDescriptions(Console.Out);
                Console.WriteLine("[*] Example: SharpPrinter.exe --AddressDump+");
                return;
            }


            try
            {
                if (AddressDump == true)
                {
                    Task task = Task.Run(() => ScanPrinters());
                    task.Wait();
                    Thread.Sleep(2000);
                    AddressBooks data = new AddressBooks();
                    foreach (string a in AddressBooks.AddressList)
                        if (a != null)
                        {
                            Console.WriteLine(a);
                        }
                        else
                        {
                            AddressBooks.AddressList.ForEach(i => Console.WriteLine("{0}\t", i));
                            Console.WriteLine("");
                        }
                }
                else
                {
                    Task task = Task.Run(() => ScanPrinters());
                    task.Wait();
                    Thread.Sleep(2000);
                    Printers data = new Printers();


                    foreach (string p in Printers.PrinterList)
                        if (p != null)
                        {
                            Match passback = Regex.Match(p, @"\b(Aficio MP|Sharp MX|ColorQube 9303)\b");
                            if (passback.Success)
                            {
                                Console.WriteLine("Found printer with potential LDAP passback: '{0}'.", p);
                                Console.WriteLine("");
                            }
                            Match export = Regex.Match(p, @"\b(iR-ADV|Minolta|KYOCERA Document Solutions Printing System|KYOCERA MITA Printing System)\b");
                            if (export.Success)
                            {
                                Console.WriteLine("Found printer with potential for passwords in address book: '{0}'.", p);
                                Console.WriteLine("");
                            }
                            Match leakage = Regex.Match(p, @"\b(M3035|KONICA MINOLTA magicolor 4690MF|KONICA MINOLTA magicolor 1690MF)\b");
                            if (leakage.Success)
                            {
                                Console.WriteLine("Found printer with potential for password leakage: '{0}'.", p);
                                Console.WriteLine("");
                            }
                        }
                        else
                        {
                            Printers.PrinterList.ForEach(i => Console.WriteLine("{0}\t", i));
                            Console.WriteLine("");
                        }
                    
                }
                Console.WriteLine("Done!");
                Console.WriteLine("For more information on these attacks, check out the following information:");
                Console.WriteLine("https://www.defcon.org/images/defcon-19/dc-19-presentations/Heiland/DEFCON-19-Heiland-Printer-To-Pwnd.pdf");
                Console.WriteLine("");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
   
        }
    }
}