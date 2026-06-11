using System.Collections.Generic;
using ADFSDump.RelyingPartyTrust;

namespace ADFSDump
{
    public class SigningKeyInfo
    {
        public string EncryptedPfx { get; set; }
        public string CertificateValue { get; set; }
        public string StoreLocationValue { get; set; }
        public string StoreNameValue { get; set; }
    }

    public class DumpResult
    {
        public List<string> DkmKeys { get; set; } = new List<string>();
        public SigningKeyInfo SigningKey { get; set; }
        public string IssuerIdentifier { get; set; }
        public List<RelyingParty> RelyingParties { get; set; } = new List<RelyingParty>();
    }
}
