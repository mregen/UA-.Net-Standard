﻿/* ========================================================================
 * Copyright (c) 2005-2017 The OPC Foundation, Inc. All rights reserved.
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

using Opc.Ua.Configuration;
using Opc.Ua.Gds.Client;
using System;
using System.Threading.Tasks;


namespace Opc.Ua.Gds.Test
{

    public class GlobalDiscoveryTestClient
    {
        GlobalDiscoveryServerClient m_gdsClient;
        ServerPushConfigurationClient m_pushClient;

        static bool autoAccept = false;

        public GlobalDiscoveryServerClient GDSClient { get { return m_gdsClient;  } }
        public ServerPushConfigurationClient PushClient { get { return m_pushClient; } }

        public GlobalDiscoveryTestClient(bool _autoAccept)
        {
            autoAccept = _autoAccept;
        }

        public async Task ConnectClient(bool gdsClient = true)
        {
            ApplicationInstance.MessageDlg = new ApplicationMessageDlg();
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "Global Discovery Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Ua.GlobalDiscoveryTestClient"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);

            // use same server for gds and push tests
            GlobalDiscoveryClientConfiguration gdsClientConfiguration = application.ApplicationConfiguration.ParseExtension<GlobalDiscoveryClientConfiguration>();
            if (gdsClient)
            {
                // get the configuration.
                m_gdsClient = new GlobalDiscoveryServerClient(application, gdsClientConfiguration);
                m_gdsClient.EndpointUrl = gdsClientConfiguration.GlobalDiscoveryServerUrl;
            }
            else
            {
                m_pushClient = new ServerPushConfigurationClient(application);
                m_pushClient.EndpointUrl = gdsClientConfiguration.GlobalDiscoveryServerUrl;
            }
        }

        public void DisconnectClient()
        {
            Console.WriteLine("Disconnect Session. Waiting for exit...");

            if (m_gdsClient != null)
            {
                GlobalDiscoveryServerClient gdsClient = m_gdsClient;
                m_gdsClient = null;
                gdsClient.Disconnect();
            }

            if (m_pushClient != null)
            {
                ServerPushConfigurationClient pushClient = m_pushClient;
                m_pushClient = null;
                pushClient.Disconnect();
            }

        }


        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }


    }
}