/* ========================================================================
 * Copyright (c) 2005-2018 The OPC Foundation, Inc. All rights reserved.
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NUnit.Framework;

namespace Opc.Ua.Security.Certificates.Tests
{
    /// <summary>
    /// Tests for the CertificateFactory class.
    /// </summary>
    [TestFixture, Category("Certificate")]
    [Parallelizable]
    [SetCulture("en-us")]
    public class CertificateTests
    {
        #region DataPointSources
        public const string Subject = "CN=Test Cert Subject";

        [DatapointSource]
        public CertificateAsset[] CertificateTestCases = new AssetCollection<CertificateAsset>(Directory.EnumerateFiles("./Assets", "*.der")).ToArray();

        [DatapointSource]
        public KeyHashPair[] KeyHashPairs = new KeyHashPairCollection {
            { 1024, HashAlgorithmName.SHA256 /* SHA-1 is deprecated HashAlgorithmName.SHA1*/ },
            { 2048, HashAlgorithmName.SHA256 },
            { 3072, HashAlgorithmName.SHA384 },
            { 4096, HashAlgorithmName.SHA512 } }.ToArray();

#if !NET462
        [DatapointSource]
        public ECCurve[] NamedCurves = typeof(ECCurve.NamedCurves).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(x => ECCurve.CreateFromFriendlyName(x.Name)).ToArray();
#endif
        #endregion

        #region Test Setup
        /// <summary>
        /// Set up a Global Discovery Server and Client instance and connect the session
        /// </summary>
        [OneTimeSetUp]
        protected void OneTimeSetUp()
        {
        }

        /// <summary>
        /// Clean up the Test PKI folder
        /// </summary>
        [OneTimeTearDown]
        protected void OneTimeTearDown()
        {
        }
        #endregion

        #region Test Methods
        /// <summary>
        /// Verify self signed app certs.
        /// </summary>
        [Test]
        public void VerifyOneSelfSignedAppCertForAll()
        {
            var builder = new CertificateBuilder(Subject)
                .SetNotBefore(DateTime.Today.AddYears(-1))
                .SetNotAfter(DateTime.Today.AddYears(25))
                .AddExtension(new X509SubjectAltNameExtension("urn:opcfoundation.org:mypc", new string[] { "mypc", "mypc.opcfoundation.org", "192.168.1.100" }));
            foreach (var keyHash in KeyHashPairs)
            {
                var cert = builder
                    .SetHashAlgorithm(keyHash.HashAlgorithmName)
                    .SetRSAKeySize(keyHash.KeySize)
                    .CreateForRSA();
                Assert.NotNull(cert);
                WriteCertificate(cert, $"Default cert with RSA {keyHash.KeySize} {keyHash.HashAlgorithmName} signature.");
                Assert.AreEqual(keyHash.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));
            }
        }

        [Theory]
        public void CreateSelfSignedForRSATests(
            KeyHashPair keyHashPair
            )
        {
            // default cert with custom key
            X509Certificate2 cert = new CertificateBuilder(Subject)
                .SetRSAKeySize(keyHashPair.KeySize)
                .CreateForRSA();
            WriteCertificate(cert, $"Default RSA {keyHashPair.KeySize} cert");
            Assert.AreEqual(keyHashPair.KeySize, cert.GetRSAPublicKey().KeySize);
            Assert.AreEqual(Defaults.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));

            // default cert with custom HashAlgorithm
            cert = new CertificateBuilder(Subject)
                .SetHashAlgorithm(keyHashPair.HashAlgorithmName)
                .CreateForRSA();
            Assert.NotNull(cert);
            WriteCertificate(cert, $"Default RSA {keyHashPair.HashAlgorithmName} cert");
            Assert.AreEqual(Defaults.RSAKeySize, cert.GetRSAPublicKey().KeySize);
            Assert.AreEqual(keyHashPair.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));

            // set dates
            cert = new CertificateBuilder(Subject)
                .SetNotBefore(DateTime.Today.AddYears(-1))
                .SetNotAfter(DateTime.Today.AddYears(25))
                .AddExtension(new X509SubjectAltNameExtension("urn:opcfoundation.org:mypc", new string[] { "mypc", "mypc.opcfoundation.org", "192.168.1.100" }))
                .SetRSAKeySize(keyHashPair.KeySize)
                .CreateForRSA();
            Assert.NotNull(cert);
            WriteCertificate(cert, $"Default cert RSA {keyHashPair.KeySize} with modified lifetime and alt name extension");
            Assert.AreEqual(keyHashPair.KeySize, cert.GetRSAPublicKey().KeySize);
            Assert.AreEqual(Defaults.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));

            // set hash algorithm
            cert = new CertificateBuilder(Subject)
                .SetHashAlgorithm(keyHashPair.HashAlgorithmName)
                .SetRSAKeySize(keyHashPair.KeySize)
                .CreateForRSA();
            Assert.NotNull(cert);
            WriteCertificate(cert, $"Default cert with RSA {keyHashPair.KeySize} {keyHashPair.HashAlgorithmName} signature.");
            Assert.AreEqual(keyHashPair.KeySize, cert.GetRSAPublicKey().KeySize);
            Assert.AreEqual(keyHashPair.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));
            cert = new CertificateBuilder(Subject)
                .SetCAConstraint(-1)
                .SetHashAlgorithm(keyHashPair.HashAlgorithmName)
                .AddExtension(X509Extensions.BuildX509CRLDistributionPoints("http://myca/mycert.crl"))
                .SetRSAKeySize(keyHashPair.KeySize)
                .CreateForRSA();
            Assert.NotNull(cert);
            WriteCertificate(cert, "Default cert with RSA {keyHashPair.KeySize} {keyHashPair.HashAlgorithmName} and CRL distribution points");
            Assert.AreEqual(keyHashPair.KeySize, cert.GetRSAPublicKey().KeySize);
            Assert.AreEqual(keyHashPair.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));
        }

        [Test]
        public void CreateSelfSignedForRSADefaultTest()
        {
            // default cert
            X509Certificate2 cert = new CertificateBuilder(Subject).CreateForRSA();
            Assert.NotNull(cert);
            WriteCertificate(cert, "Default RSA cert");
            Assert.NotNull(cert.GetRSAPrivateKey());
            var publicKey = cert.GetRSAPublicKey();
            Assert.NotNull(publicKey);
            Assert.AreEqual(Defaults.RSAKeySize, publicKey.KeySize);
            Assert.AreEqual(Defaults.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));
            Assert.AreEqual(DateTime.UtcNow.AddDays(-1).Date, cert.NotBefore.ToUniversalTime());
            Assert.AreEqual(cert.NotBefore.ToUniversalTime().AddMonths(Defaults.LifeTime), cert.NotAfter.ToUniversalTime());
            X509Utils.VerifyRSAKeyPair(cert, cert);
            X509Utils.VerifySelfSigned(cert);
        }

        [Test]
        public void CreateRSADefaultWithSerialTest()
        {
            // default cert
            Assert.Throws<ArgumentOutOfRangeException>(
                () => {
                    var cert = new CertificateBuilder(Subject)
                    .SetSerialNumberLength(0)
                    .CreateForRSA();
                }
            );
            Assert.Throws<ArgumentOutOfRangeException>(
                () => {
                    var cert = new CertificateBuilder(Subject)
                    .SetSerialNumberLength(Defaults.SerialNumberLengthMax + 1)
                    .CreateForRSA();
                }
            );
            var builder = new CertificateBuilder(Subject)
                .SetSerialNumberLength(Defaults.SerialNumberLengthMax);

            // ensure every cert has a different serial number
            var cert1 = builder.CreateForRSA();
            var cert2 = builder.CreateForRSA();
            WriteCertificate(cert1, "Cert1 with max length serial number");
            WriteCertificate(cert2, "Cert2 with max length serial number");
            Assert.AreEqual(Defaults.SerialNumberLengthMax, cert1.GetSerialNumber().Length);
            Assert.AreEqual(cert1.SerialNumber.Length, cert2.SerialNumber.Length);
            Assert.AreEqual(cert1.GetSerialNumber().Length, cert2.GetSerialNumber().Length);
            Assert.AreNotEqual(cert1.SerialNumber, cert2.SerialNumber);
        }

        [Test]
        public void CreateRSAManualSerialTest()
        {
            // default cert
            Assert.Throws<ArgumentOutOfRangeException>(
                () => {
                    var cert = new CertificateBuilder(Subject)
                    .SetSerialNumber(new byte[0])
                    .CreateForRSA();
                }
            );
            Assert.Throws<ArgumentOutOfRangeException>(
                () => {
                    var cert = new CertificateBuilder(Subject)
                    .SetSerialNumber(new byte[Defaults.SerialNumberLengthMax + 1])
                    .CreateForRSA();
                }
            );
            var serial = new byte[Defaults.SerialNumberLengthMax];
            for (int i = 0; i < serial.Length; i++)
            {
                serial[i] = (byte)((i + 1) | 0x80);
            }

            // test if sign bit is cleared
            var builder = new CertificateBuilder(Subject)
                .SetSerialNumber(serial);
            serial[serial.Length - 1] &= 0x7f;
            Assert.AreEqual(serial, builder.GetSerialNumber());
            var cert1 = builder.CreateForRSA();
            WriteCertificate(cert1, "Cert1 with max length serial number");

            // clear sign bit
            builder.SetSerialNumber(serial);
            Assert.AreEqual(serial, builder.GetSerialNumber());

            var cert2 = builder.CreateForRSA();
            WriteCertificate(cert2, "Cert2 with max length serial number");
            TestContext.Out.WriteLine($"Serial: {serial.ToHexString(true)}");

            Assert.AreEqual(Defaults.SerialNumberLengthMax, cert1.GetSerialNumber().Length);
            Assert.AreEqual(cert1.SerialNumber.Length, cert2.SerialNumber.Length);
            Assert.AreEqual(cert1.SerialNumber, cert2.SerialNumber);
            Assert.AreEqual(Defaults.SerialNumberLengthMax, cert2.GetSerialNumber().Length);
            Assert.AreEqual(serial, cert1.GetSerialNumber());
            Assert.AreEqual(serial, cert2.GetSerialNumber());
        }


#if NETCOREAPP3_1
        [Theory]
        public void CreateSelfSignedForECDsaTests(ECCurve eccurve)
        {
            // default cert
            X509Certificate2 cert = new CertificateBuilder(Subject).SetECCurve(eccurve).CreateForECDsa();
            WriteCertificate(cert, "Default ECDsa cert");
            // set dates
            cert = new CertificateBuilder(Subject)
                .SetNotBefore(DateTime.Today.AddYears(-1))
                .SetNotAfter(DateTime.Today.AddYears(25))
                .AddExtension(new X509SubjectAltNameExtension("urn:opcfoundation.org:mypc", new string[] { "mypc", "mypc.opcfoundation.org", "192.168.1.100" }))
                .SetECCurve(eccurve)
                .CreateForECDsa();
            WriteCertificate(cert, "Default cert with modified lifetime and alt name extension");
            // set hash alg
            cert = new CertificateBuilder(Subject)
                .SetHashAlgorithm(HashAlgorithmName.SHA512)
                .SetECCurve(eccurve)
                .CreateForECDsa();
            WriteCertificate(cert, "Default cert with SHA512 signature.");
            // set CA constraints
            cert = new CertificateBuilder(Subject)
                .SetCAConstraint(-1)
                .AddExtension(X509Extensions.BuildX509CRLDistributionPoints("http://myca/mycert.crl"))
                .SetECCurve(eccurve)
                .CreateForECDsa();
            WriteCertificate(cert, "Default cert with CA constraints None and CRL distribution points");
        }
#endif

        [Test]
        public void CreateForRSAWithGeneratorTest(
            //KeyHashPair keyHashPair
            )
        {
            // default cert with custom key
            X509Certificate2 signingCert = new CertificateBuilder(Subject)
                .SetCAConstraint()
                .CreateForRSA();
            WriteCertificate(signingCert, $"Signing RSA {signingCert.GetRSAPublicKey().KeySize} cert");

            using (RSA rsa = signingCert.GetRSAPrivateKey())
            {
                var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
                var cert = new CertificateBuilder("CN=App Cert")
                    .CreateForRSA(generator);
                Assert.NotNull(cert);
                WriteCertificate(cert, $"Default signed RSA cert");
            }

            //Assert.AreEqual(Defaults.RSAKeySize, cert.GetRSAPublicKey().KeySize);
            //Assert.AreEqual(keyHashPair.HashAlgorithmName, Oids.GetHashAlgorithmName(cert.SignatureAlgorithm.Value));
        }
        #endregion

        #region Private Methods
        private void WriteCertificate(X509Certificate2 cert, string message)
        {
            TestContext.Out.WriteLine(message);
            TestContext.Out.WriteLine(cert);
            foreach (var ext in cert.Extensions)
            {
                TestContext.Out.WriteLine(ext.Format(false));
            }
        }
        #endregion

        #region Private Fields
        #endregion
    }

}
