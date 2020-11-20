/* Copyright (c) 1996-2020 The OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation members in good-standing
     - GPL V2: everybody else
   RCL license terms accompanied with this source code. See http://opcfoundation.org/License/RCL/1.00/
   GNU General Public License as published by the Free Software Foundation;
   version 2 of the License are accompanied with this source code. See http://opcfoundation.org/License/GPLv2
   This source code is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Opc.Ua.Security.Certificates;

namespace Opc.Ua
{
    public static class X509Utils
    {
        /// <summary>
        /// Extracts the DNS names specified in the certificate.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <returns>The DNS names.</returns>
        public static IList<string> GetDomainsFromCertficate(X509Certificate2 certificate)
        {
            List<string> dnsNames = new List<string>();

            // extracts the domain from the subject name.
            List<string> fields = X509Utils.ParseDistinguishedName(certificate.Subject);

            StringBuilder builder = new StringBuilder();

            for (int ii = 0; ii < fields.Count; ii++)
            {
                if (fields[ii].StartsWith("DC="))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('.');
                    }

                    builder.Append(fields[ii].Substring(3));
                }
            }

            if (builder.Length > 0)
            {
                dnsNames.Add(builder.ToString().ToUpperInvariant());
            }

            // extract the alternate domains from the subject alternate name extension.
            X509SubjectAltNameExtension alternateName = X509Extensions.FindExtension<X509SubjectAltNameExtension>(certificate);
            if (alternateName != null)
            {
                for (int ii = 0; ii < alternateName.DomainNames.Count; ii++)
                {
                    string hostname = alternateName.DomainNames[ii];

                    // do not add duplicates to the list.
                    bool found = false;

                    for (int jj = 0; jj < dnsNames.Count; jj++)
                    {
                        if (String.Compare(dnsNames[jj], hostname, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        dnsNames.Add(hostname.ToUpperInvariant());
                    }
                }

                for (int ii = 0; ii < alternateName.IPAddresses.Count; ii++)
                {
                    string ipAddress = alternateName.IPAddresses[ii];

                    if (!dnsNames.Contains(ipAddress))
                    {
                        dnsNames.Add(ipAddress);
                    }
                }
            }

            // return the list.
            return dnsNames;
        }

        /// <summary>
        /// Returns the size of the public key and disposes RSA key.
        /// </summary>
        /// <param name="certificate">The certificate</param>
        public static int GetRSAPublicKeySize(X509Certificate2 certificate)
        {
            RSA rsaPublicKey = null;
            try
            {
                rsaPublicKey = certificate.GetRSAPublicKey();
                return rsaPublicKey.KeySize;
            }
            finally
            {
                RsaUtils.RSADispose(rsaPublicKey);
            }
        }

        /// <summary>
        /// Extracts the application URI specified in the certificate.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <returns>The application URI.</returns>
        public static string GetApplicationUriFromCertificate(X509Certificate2 certificate)
        {
            // extract the alternate domains from the subject alternate name extension.
            X509SubjectAltNameExtension alternateName = X509Extensions.FindExtension<X509SubjectAltNameExtension>(certificate);

            // get the application uri.
            if (alternateName != null && alternateName.Uris.Count > 0)
            {
                return alternateName.Uris[0];
            }

            return string.Empty;
        }

        // TODO: remove function?
        /// <summary>
        /// Check if certificate has an application urn.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <returns>true if the application URI starts with urn: </returns>
        public static bool HasApplicationURN(X509Certificate2 certificate)
        {
            // extract the alternate domains from the subject alternate name extension.
            X509SubjectAltNameExtension alternateName = X509Extensions.FindExtension<X509SubjectAltNameExtension>(certificate);

            // find the application urn.
            if (alternateName != null && alternateName.Uris.Count > 0)
            {
                string urn = "urn:";
                for (int i = 0; i < alternateName.Uris.Count; i++)
                {
                    if (string.Compare(alternateName.Uris[i], 0, urn, 0, urn.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks that the domain in the URL provided matches one of the domains in the certificate.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <param name="endpointUrl">The endpoint url to verify.</param>
        /// <returns>True if the certificate matches the url.</returns>
        public static bool DoesUrlMatchCertificate(X509Certificate2 certificate, Uri endpointUrl)
        {
            if (endpointUrl == null || certificate == null)
            {
                return false;
            }

            IList<string> domainNames = GetDomainsFromCertficate(certificate);

            for (int jj = 0; jj < domainNames.Count; jj++)
            {
                if (String.Compare(domainNames[jj], endpointUrl.DnsSafeHost, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the certificate is allowed to be an issuer.
        /// </summary>
        public static bool IsIssuerAllowed(X509Certificate2 certificate)
        {
            X509BasicConstraintsExtension constraints = X509Extensions.FindExtension<X509BasicConstraintsExtension>(certificate);

            if (constraints != null)
            {
                return constraints.CertificateAuthority;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the certificate is issued by a Certificate Authority.
        /// </summary>
        public static bool IsCertificateAuthority(X509Certificate2 certificate)
        {
            var constraints = X509Extensions.FindExtension<X509BasicConstraintsExtension>(certificate);
            if (constraints != null)
            {
                return constraints.CertificateAuthority;
            }
            return false;
        }

        /// <summary>
        /// Return the key usage flags of a certificate.
        /// </summary>
        public static X509KeyUsageFlags GetKeyUsage(X509Certificate2 cert)
        {
            var allFlags = X509KeyUsageFlags.None;
            foreach (X509KeyUsageExtension ext in cert.Extensions.OfType<X509KeyUsageExtension>())
            {
                allFlags |= ext.KeyUsages;
            }
            return allFlags;
        }

        /// <summary>
        /// Compares two distinguished names.
        /// </summary>
        public static bool CompareDistinguishedName(string name1, string name2)
        {
            // check for simple equality.
            if (String.Compare(name1, name2, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            // parse the names.
            List<string> fields1 = ParseDistinguishedName(name1);
            List<string> fields2 = ParseDistinguishedName(name2);

            // can't be equal if the number of fields is different.
            if (fields1.Count != fields2.Count)
            {
                return false;
            }

            // sort to ensure similar entries are compared
            fields1.Sort(StringComparer.OrdinalIgnoreCase);
            fields2.Sort(StringComparer.OrdinalIgnoreCase);

            // compare each.
            for (int ii = 0; ii < fields1.Count; ii++)
            {
                if (String.Compare(fields1[ii], fields2[ii], StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two distinguished names.
        /// </summary>
        public static bool CompareDistinguishedName(X509Certificate2 certificate, List<string> parsedName)
        {
            // can't compare if the number of fields is 0.
            if (parsedName.Count == 0)
            {
                return false;
            }

            // parse the names.
            List<string> certificateName = ParseDistinguishedName(certificate.Subject);

            // can't be equal if the number of fields is different.
            if (parsedName.Count != certificateName.Count)
            {
                return false;
            }

            // sort to ensure similar entries are compared
            parsedName.Sort(StringComparer.OrdinalIgnoreCase);
            certificateName.Sort(StringComparer.OrdinalIgnoreCase);

            // compare each entry
            for (int ii = 0; ii < parsedName.Count; ii++)
            {
                if (String.Compare(parsedName[ii], certificateName[ii], StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a distingushed name.
        /// </summary>
        public static List<string> ParseDistinguishedName(string name)
        {
            List<string> fields = new List<string>();

            if (String.IsNullOrEmpty(name))
            {
                return fields;
            }

            // determine the delimiter used.
            char delimiter = ',';
            bool found = false;
            bool quoted = false;

            for (int ii = name.Length - 1; ii >= 0; ii--)
            {
                char ch = name[ii];

                if (ch == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && ch == '=')
                {
                    ii--;

                    while (ii >= 0 && Char.IsWhiteSpace(name[ii])) ii--;
                    while (ii >= 0 && (Char.IsLetterOrDigit(name[ii]) || name[ii] == '.')) ii--;
                    while (ii >= 0 && Char.IsWhiteSpace(name[ii])) ii--;

                    if (ii >= 0)
                    {
                        delimiter = name[ii];
                    }

                    break;
                }
            }

            StringBuilder buffer = new StringBuilder();

            string key = null;
            string value = null;
            found = false;

            for (int ii = 0; ii < name.Length; ii++)
            {
                while (ii < name.Length && Char.IsWhiteSpace(name[ii])) ii++;

                if (ii >= name.Length)
                {
                    break;
                }

                char ch = name[ii];

                if (found)
                {
                    char end = delimiter;

                    if (ii < name.Length && name[ii] == '"')
                    {
                        ii++;
                        end = '"';
                    }

                    while (ii < name.Length)
                    {
                        ch = name[ii];

                        if (ch == end)
                        {
                            while (ii < name.Length && name[ii] != delimiter) ii++;
                            break;
                        }

                        buffer.Append(ch);
                        ii++;
                    }

                    value = buffer.ToString().TrimEnd();
                    found = false;

                    buffer.Length = 0;
                    buffer.Append(key);
                    buffer.Append('=');

                    if (value.IndexOfAny(new char[] { '/', ',', '=' }) != -1)
                    {
                        if (value.Length > 0 && value[0] != '"')
                        {
                            buffer.Append('"');
                        }

                        buffer.Append(value);

                        if (value.Length > 0 && value[value.Length - 1] != '"')
                        {
                            buffer.Append('"');
                        }
                    }
                    else
                    {
                        buffer.Append(value);
                    }

                    fields.Add(buffer.ToString());
                    buffer.Length = 0;
                }

                else
                {
                    while (ii < name.Length)
                    {
                        ch = name[ii];

                        if (ch == '=')
                        {
                            break;
                        }

                        buffer.Append(ch);
                        ii++;
                    }

                    key = buffer.ToString().TrimEnd().ToUpperInvariant();
                    buffer.Length = 0;
                    found = true;
                }
            }

            return fields;
        }

        /// <summary>
        /// Verify RSA key pair of two certificates.
        /// </summary>
        public static bool VerifyRSAKeyPair(
            X509Certificate2 certWithPublicKey,
            X509Certificate2 certWithPrivateKey,
            bool throwOnError = false)
        {
            bool result = false;
            RSA rsaPrivateKey = null;
            RSA rsaPublicKey = null;
            try
            {
                // verify the public and private key match
                rsaPrivateKey = certWithPrivateKey.GetRSAPrivateKey();
#if NETSTANDARD2_1
                // on .NET Core 3 
                rsaPrivateKey.ExportParameters(true);
#endif
                rsaPublicKey = certWithPublicKey.GetRSAPublicKey();
                X509KeyUsageFlags keyUsage = GetKeyUsage(certWithPublicKey);
                if ((keyUsage & X509KeyUsageFlags.DataEncipherment) != 0)
                {
                    result = RsaUtils.VerifyRSAKeyPairCrypt(rsaPublicKey, rsaPrivateKey);
                }
                else if ((keyUsage & X509KeyUsageFlags.DigitalSignature) != 0)
                {
                    result = RsaUtils.VerifyRSAKeyPairSign(rsaPublicKey, rsaPrivateKey);
                }
                else
                {
                    throw new CryptographicException("Don't know how to verify the public/private key pair.");
                }
            }
            catch (Exception)
            {
                if (throwOnError)
                {
                    throwOnError = false;
                    throw;
                }
            }
            finally
            {
                RsaUtils.RSADispose(rsaPrivateKey);
                RsaUtils.RSADispose(rsaPublicKey);
                if (!result && throwOnError)
                {
                    throw new CryptographicException("The public/private key pair in the certficates do not match.");
                }
            }
            return result;
        }

        /// <summary>
        /// Verify the signature of a self signed certificate.
        /// </summary>
        public static bool VerifySelfSigned(X509Certificate2 cert)
        {
            try
            {
                //TODO
                Org.BouncyCastle.X509.X509Certificate bcCert = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(cert.RawData);
                bcCert.Verify(bcCert.GetPublicKey());
            }
            catch
            {
                return false;
            }
            return true;
        }

    }
}
