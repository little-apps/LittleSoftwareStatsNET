﻿/*
 * Little Software Stats - .NET Library
 * Copyright (C) 2008-2012 Little Apps (http://www.little-apps.org)
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Collections;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO;
using Microsoft.Win32;

namespace LittleSoftwareStats
{
    internal class Utils
    {
        public static string GetCommandExecutionOutput(string command, string arguments)
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = command,
                    Arguments = arguments
                }
            };

            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();

            if (String.IsNullOrEmpty(output))
                output = proc.StandardError.ReadToEnd();

            return output;
        }

        public static int GetUnixTime()
        {
            return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        public static object GetRegistryValue(RegistryKey regRoot, string regPath, string valueName, object defaultValue = null)
        {
            object value = defaultValue;

            using (RegistryKey regKey = regRoot.OpenSubKey(regPath))
            {
                if (regKey != null)
                {
                    value = defaultValue != null ? regKey.GetValue(valueName, defaultValue) : regKey.GetValue(valueName);
                }
            }

            return value;
        }

        public static string SerializeAsXml(Events events)
        {
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                CloseOutput = false
            };

            StringBuilder sb = new StringBuilder();
            XmlWriter xmlWriter = XmlWriter.Create(sb, settings);

            xmlWriter.WriteStartElement("Events");

            foreach (Event e in events)
            {
                xmlWriter.WriteStartElement("Event");

                foreach (DictionaryEntry de in e)
                {
                    string name = de.Key as string;
                    var value = de.Value;

                    if (value != null)
                    {
                        xmlWriter.WriteStartElement(name);
                        xmlWriter.WriteCData(value.ToString());
                        xmlWriter.WriteEndElement();
                    } 
                    else
                        xmlWriter.WriteElementString(name, "");
                }

                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.Close();

            return sb.ToString();
        }

        public static string SerializeAsJson(Events events)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;

            sb.Append("[");

            foreach (Event e in events)
            {
                sb.Append("{");

                int j = 0;

                foreach (DictionaryEntry de in e)
                {
                    string name = de.Key as string;
                    var value = de.Value;

                    sb.Append("\"" + name + "\":");

                    if (value == null)
                        sb.Append("null");
                    else if (value is string)
                        sb.Append("\"" + value + "\"");
                    else if (value is Version)
                        sb.Append("\"" + value + "\"");
                    else
                        sb.Append("\"" + value + "\"");

                    if (j++ < e.Count - 1)
                        sb.Append(',');
                }

                sb.Append("}");

                if (i++ < events.Count - 1)
                    sb.Append(",");
            }

            sb.Append("]");

            return sb.ToString();
        }

        /// <summary>
        /// The method create a Base64 encoded string from a normal string.
        /// </summary>
        /// <param name="toEncode">The String containing the characters to encode.</param>
        /// <returns>The Base64 encoded string.</returns>
        public static string EncodeTo64(string toEncode)
        {
            try
            {
                byte[] toEncodeAsBytes = Encoding.Unicode.GetBytes(toEncode);
                string returnValue = Convert.ToBase64String(toEncodeAsBytes);
                return returnValue;
            }
            catch
            {
                return "";
            }

        }

        /// <summary>
        /// The method to Decode your Base64 strings.
        /// </summary>
        /// <param name="encodedData">The String containing the characters to decode.</param>
        /// <returns>A String containing the results of decoding the specified sequence of bytes.</returns>
        public static string DecodeFrom64(string encodedData)
        {
            try
            {
                byte[] encodedDataAsBytes = Convert.FromBase64String(encodedData);
                string returnValue = Encoding.Unicode.GetString(encodedDataAsBytes);
                return returnValue;
            }
            catch
            {
                return "";
            }
        }

        public static void SendPostData(string postData)
        {
            if (Config.ApiSecure)
            {
                ServicePointManager.ServerCertificateValidationCallback +=
                        delegate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslError)
                        {
                            bool validationResult = true;
                            return validationResult;
                        };
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Config.ApiUrl);

            request.UserAgent = Config.ApiUserAgent;
            request.KeepAlive = false;
            request.ProtocolVersion = HttpVersion.Version10;
            request.Method = "POST";
            request.Timeout = Config.ApiTimeout;

#if DEBUG
            Console.WriteLine(postData);
#endif

            // Get POST data and convert it to UTF8
            byte[] byteArray = Encoding.UTF8.GetBytes("data=" + postData);

            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;

            try
            {
                // Send the request
                Stream dataStream;

                using (dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                // Get the response
                using (WebResponse response = request.GetResponse())
                {
                    using (dataStream = response.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(dataStream);

#if DEBUG
                        Console.WriteLine(reader.ReadToEnd());
#endif
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine("Error: " + ex);
            }
        }

#region MacOSX Functions
        private static string _systemProfiler;
        public static string SystemProfilerCommandOutput
        {
            get
            {
                if (string.IsNullOrEmpty(_systemProfiler))
                    _systemProfiler = GetCommandExecutionOutput("system_profiler", "");
                return _systemProfiler;
            }
        }

        private static string _sysctl;
        public static string SysctlCommandOutput
        {
            get
            {
                if (string.IsNullOrEmpty(_sysctl))
                    _sysctl = Utils.GetCommandExecutionOutput("sysctl", "-a hw");
                return _sysctl;
            }
        }
#endregion
    }
}
