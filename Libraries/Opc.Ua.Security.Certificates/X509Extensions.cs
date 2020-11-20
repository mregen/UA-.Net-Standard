/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Opc.Ua.Security.Certificates
{
    public static class X509Extensions
    {
        public static T FindExtension<T>(X509Certificate2 certificate) where T : X509Extension
        {
            // search known custom extensions
            if (typeof(T) == typeof(X509AuthorityKeyIdentifierExtension))
            {
                var extension = certificate.Extensions.Cast<X509Extension>().Where(e => (
                    e.Oid.Value == X509AuthorityKeyIdentifierExtension.AuthorityKeyIdentifierOid ||
                    e.Oid.Value == X509AuthorityKeyIdentifierExtension.AuthorityKeyIdentifier2Oid)
                ).FirstOrDefault();
                if (extension != null)
                {
                    return new X509AuthorityKeyIdentifierExtension(extension, extension.Critical) as T;
                }
            }

            if (typeof(T) == typeof(X509SubjectAltNameExtension))
            {
                var extension = certificate.Extensions.Cast<X509Extension>().Where(e => (
                    e.Oid.Value == X509SubjectAltNameExtension.SubjectAltNameOid ||
                    e.Oid.Value == X509SubjectAltNameExtension.SubjectAltName2Oid)
                ).FirstOrDefault();
                if (extension != null)
                {
                    return new X509SubjectAltNameExtension(extension, extension.Critical) as T;
                }
            }

            // search builtin extension
            return certificate.Extensions.OfType<T>().FirstOrDefault();
        }


        /// <summary>
        /// Build the Authority information Access extension.
        /// </summary>
        /// <param name="caIssuerUrls">Array of CA Issuer Urls</param>
        /// <param name="ocspResponder">optional, the OCSP responder </param>
        public static X509Extension BuildX509AuthorityInformationAccess(
            string[] caIssuerUrls,
            string ocspResponder = null
            )
        {
            if (String.IsNullOrEmpty(ocspResponder) &&
               (caIssuerUrls == null || caIssuerUrls.Length == 0))
            {
                throw new ArgumentNullException(nameof(caIssuerUrls), "One CA Issuer Url or OCSP responder is required for the extension.");
            }

            var context0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
            Asn1Tag generalNameUriChoice = new Asn1Tag(TagClass.ContextSpecific, 6);
            {
                AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);
                writer.PushSequence();
                if (caIssuerUrls != null)
                {
                    foreach (var caIssuerUrl in caIssuerUrls)
                    {
                        writer.PushSequence();
                        writer.WriteObjectIdentifier("1.3.6.1.5.5.7.48.2");
                        writer.WriteCharacterString(
                            UniversalTagNumber.IA5String,
                            caIssuerUrl,
                            generalNameUriChoice
                            );
                        writer.PopSequence();
                    }
                }
                if (!String.IsNullOrEmpty(ocspResponder))
                {
                    writer.PushSequence();
                    writer.WriteObjectIdentifier("1.3.6.1.5.5.7.48.1");
                    writer.WriteCharacterString(
                        UniversalTagNumber.IA5String,
                        ocspResponder,
                        generalNameUriChoice
                        );
                    writer.PopSequence();
                }
                writer.PopSequence();
                return new X509Extension("1.3.6.1.5.5.7.1.1", writer.Encode(), false);
            }
        }

#if NETSTANDARD2_1
        /// <summary>
        /// Build the Subject Alternative name extension (for OPC UA application certs)
        /// </summary>
        /// <param name="applicationUri">The application Uri</param>
        /// <param name="domainNames">The domain names. DNS Hostnames, IPv4 or IPv6 addresses</param>
        public static X509Extension BuildSubjectAlternativeName(string applicationUri, IList<string> domainNames)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddUri(new Uri(applicationUri));
            foreach (string domainName in domainNames)
            {
                IPAddress ipAddr;
                if (String.IsNullOrWhiteSpace(domainName))
                {
                    continue;
                }
                if (IPAddress.TryParse(domainName, out ipAddr))
                {
                    sanBuilder.AddIpAddress(ipAddr);
                }
                else
                {
                    sanBuilder.AddDnsName(domainName);
                }
            }

            return sanBuilder.Build();
        }
#endif

        /// <summary>
        /// Build the CRL Distribution Point extension.
        /// </summary>
        /// <param name="distributionPoint">The CRL distribution point</param>
        public static X509Extension BuildX509CRLDistributionPoints(
            string distributionPoint
            )
        {
            var context0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
            Asn1Tag distributionPointChoice = context0;
            Asn1Tag fullNameChoice = context0;
            Asn1Tag generalNameUriChoice = new Asn1Tag(TagClass.ContextSpecific, 6);

            {
                AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);
                writer.PushSequence();
                writer.PushSequence();
                writer.PushSequence(distributionPointChoice);
                writer.PushSequence(fullNameChoice);
                writer.WriteCharacterString(
                    UniversalTagNumber.IA5String,
                    distributionPoint,
                    generalNameUriChoice
                    );
                writer.PopSequence(fullNameChoice);
                writer.PopSequence(distributionPointChoice);
                writer.PopSequence();
                writer.PopSequence();
                return new X509Extension("2.5.29.31", writer.Encode(), false);
            }
        }

        public static X509Extension ReadExtension(this AsnReader reader)
        {
            if (reader.HasData)
            {
                var boolTag = new Asn1Tag(UniversalTagNumber.Boolean);
                var extReader = reader.ReadSequence();
                var extOid = extReader.ReadObjectIdentifier();
                bool critical = false;
                var peekTag = extReader.PeekTag();
                if (peekTag == boolTag)
                {
                    critical = extReader.ReadBoolean();
                }
                var data = extReader.ReadOctetString();
                return new X509Extension(new Oid(extOid), data, critical);
            }
            return null;
        }

        public static void WriteExtension(this AsnWriter writer, X509Extension extension)
        {
            var etag = Asn1Tag.Sequence;
            writer.PushSequence(etag);
            writer.WriteObjectIdentifier(extension.Oid.Value);
            if (extension.Critical)
            {
                writer.WriteBoolean(extension.Critical);
            }
            writer.WriteOctetString(extension.RawData);
            writer.PopSequence(etag);
        }

        /// <summary>
        /// Build the CRL Reason extension.
        /// </summary>
        public static X509Extension BuildX509CRLReason(
            CRLReason reason
            )
        {
            AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);
            //writer.PushSequence();
            //writer.WriteObjectIdentifier(OidConstants.CrlReasonCode);
            // TODO: is there a better way to encode CRLReason?
            writer.WriteOctetString(new byte[] { (byte)UniversalTagNumber.Enumerated, 0x1, (byte)reason });
            //writer.PopSequence();
            return new X509Extension(OidConstants.CrlReasonCode, writer.Encode(), false);
        }

        /// <summary>
        /// Build the Authority Key Identifier from an Issuer CA certificate.
        /// </summary>
        /// <param name="issuerCaCertificate">The issuer CA certificate</param>
        public static X509Extension BuildAuthorityKeyIdentifier(X509Certificate2 issuerCaCertificate)
        {
            // force exception if SKI is not present
            var ski = issuerCaCertificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single();
            return new X509AuthorityKeyIdentifierExtension(issuerCaCertificate.SubjectName,
                issuerCaCertificate.GetSerialNumber(), ski.SubjectKeyIdentifier.FromHexString());
        }

        /// <summary>
        /// Build the CRL number.
        /// </summary>
        public static X509Extension BuildCRLNumber(BigInteger crlNumber)
        {
            AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);
            writer.WriteInteger(crlNumber);
            return new X509Extension(OidConstants.CrlNumber, writer.Encode(), false);
        }
    }
}
