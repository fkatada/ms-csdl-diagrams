﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Csdl.Diagram
{
    internal static class RenderSvg
    {
        private const string defaultServerBase = "https://www.plantuml.com/plantuml";

        /// <summary>
        /// Render an Svg diagram using public PlantUML server by default.
        /// </summary>
        /// <param name="plantUml">The plantuml code to render.</param>
        /// <param name="urlBase">Base url of platuml renderer to use.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async ValueTask<byte[]> RenderSvgDiagram(string plantUml, string? urlBase = null)
        {
            using var client = new HttpClient();
            string url = $"{urlBase ?? defaultServerBase}/svg";
            HttpResponseMessage response;

            // The default public server does not support POSTing, so we have to fall back to encoded GET
            if (urlBase == null)
            {
                string encodedDiagram = CreateEncodedDiagram(plantUml);
                url += $"/{encodedDiagram}";
                response = await SendWithRetry(() => client.GetAsync(url));
            }
            else // Assumption is that explicitly-specified server has been configured to accept POST.
            {
                var content = new StringContent(plantUml);
                response = await SendWithRetry(() => client.PostAsync(url, content));
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                string errorMessage = $"Error rendering SVG file: status code: {response.StatusCode}";
                throw new InvalidOperationException(errorMessage);
            }
            else
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            static async Task<HttpResponseMessage> SendWithRetry(Func<Task<HttpResponseMessage>> task)
            {
                HttpResponseMessage response = await task();
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Retry once after delay.
                    await Task.Delay(2000);
                    response = await task();
                }

                return response;
            }
        }

        private static string CreateEncodedDiagram(string s)
        {
            var utf8 = Encoding.UTF8.GetBytes(s);
            using var deflatedMemStream = new MemoryStream();
            using (var deflatedStream = new DeflateStream(deflatedMemStream, CompressionLevel.Optimal))
            {
                deflatedStream.Write(utf8, 0, utf8.Length);
            }
            byte[] bytes = deflatedMemStream.ToArray();
            string encodedBody = Encode64(bytes);
            return encodedBody;
        }

        // Custom Base64 encoding algorithm needed by PlantUML server. :-(
        private static string Encode64(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += 3)
            {
                if (i + 2 == data.Length)
                {
                    sb.Append(Append3Bytes(Convert.ToUInt16(data[i]), Convert.ToUInt16(data[i + 1]), 0));
                }
                else if (i + 1 == data.Length)
                {
                    sb.Append(Append3Bytes(Convert.ToUInt16(data[i]), 0, 0));
                }
                else
                {
                    sb.Append(Append3Bytes(Convert.ToUInt16(data[i]),
                                    Convert.ToUInt16(data[i + 1]),
                                    Convert.ToUInt16(data[i + 2])));
                }
            }
            return sb.ToString();
        }

        private static string Append3Bytes(UInt16 b1, UInt16 b2, UInt16 b3)
        {
            UInt16 c1 = (UInt16)(b1 >> 2);
            UInt16 c2 = (UInt16)(((b1 & 0x3) << 4) | (b2 >> 4));
            UInt16 c3 = (UInt16)(((b2 & 0xF) << 2) | (b3 >> 6));
            UInt16 c4 = (UInt16)(b3 & 0x3F);
            var r = "";
            r += Encode6Bit((UInt16)(c1 & 0x3F));
            r += Encode6Bit((UInt16)(c2 & 0x3F));
            r += Encode6Bit((UInt16)(c3 & 0x3F));
            r += Encode6Bit((UInt16)(c4 & 0x3F));
            return r;
        }

        private static char Encode6Bit(UInt16 b)
        {
            if (b < 10)
            {
                return Convert.ToChar(48 + b);
            }
            b -= 10;
            if (b < 26)
            {
                return Convert.ToChar(65 + b);
            }
            b -= 26;
            if (b < 26)
            {
                return Convert.ToChar(97 + b);
            }
            b -= 26;
            if (b == 0)
            {
                return '-';
            }
            if (b == 1)
            {
                return '_';
            }
            return '?';
        }
    }
}
