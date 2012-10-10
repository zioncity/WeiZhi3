using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Weibo.Apis
{
    public static class Utils
    {
        public static string[] ExtractUrlFromWeibo(string text, string su = "http://t.cn/")
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];
            var rtn = new List<string>();
            var c = 0;
            while (true)
            {
                var fi = text.IndexOf(su, c, StringComparison.OrdinalIgnoreCase);
                if (fi < 0)
                    break;
                var x = string.Empty;
                for (fi = fi + su.Length; fi < text.Length && char.IsLetterOrDigit(text[fi]) && text[fi] < 128; ++fi)
                {
                    x += text[fi];
                }

                if (x.Length <= 0)
                {
                    break;
                }
                rtn.Add(x);
                c = fi ;
            }
            return rtn.ToArray();
        }
        internal static int LittleEndianInt32(byte[] buffer, int start)
        {
            var b = new byte[sizeof(int)];
            for (var i = 0; i < sizeof(int); ++i)
            {
                //b[i] = buffer[start + sizeof (int) - i - 1];
                b[sizeof(int) - i - 1] = buffer[start + i];
            }
            return BitConverter.ToInt32(b, 0);
        }
        internal static short LittleEndianInt16(byte[] buffer, int start)
        {
            var bytes = new byte[sizeof(short)];

            for (var i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = buffer[start + i];
            }
            return BitConverter.ToInt16(bytes, 0);
        }
        public static async Task<ImageSize> FetchImageSizeAsync(string imgurl)
        {
            var rtn = new ImageSize();
            using (var client = new HttpClient())
            {
                var resp = await client.GetAsync(imgurl, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                    return rtn;
                Stream stream = null;
                try
                {
                    stream = await resp.Content.ReadAsStreamAsync();

                    var buffer = new byte[8];
                    var read = await stream.ReadAsync(buffer, 0, 1);
                    if (read != 1)
                        return rtn;
                    if (buffer[0] == 0x42) //bitmap
                    {
                        read = await stream.ReadAsync(buffer, 0, 1);
                        if (read == 1 && buffer[0] == 0x4D)//BITMAP magic match
                        {
                            //var b = new byte[8];
                            read = await stream.ReadAsync(buffer, 0, 8);
                            if (read == 8)
                            {
                                rtn.Width = BitConverter.ToInt32(buffer, 0);
                                rtn.Height = BitConverter.ToInt32(buffer, 4);
                            }
                        }
                    }
                    else if (buffer[0] == 0x47) //gif
                    {
                        // var b = new byte[5];
                        read = await stream.ReadAsync(buffer, 0, 5);
                        if (read == 5 && buffer[0] == 0x49 && buffer[1] == 0x46 && buffer[2] == 0x38 && buffer[4] == 0x61)// gif magic match
                        {
                            await stream.ReadAsync(buffer, 0, 2);
                            rtn.Width = BitConverter.ToInt16(buffer, 0);
                            await stream.ReadAsync(buffer, 0, 2);
                            rtn.Height = BitConverter.ToInt16(buffer, 0);
                        }
                    }
                    else if (buffer[0] == 0x89) //png
                    {
                        //var b = new byte[8];
                        read = await stream.ReadAsync(buffer, 0, 7);
                        if (read == 7 && buffer[0] == 0x50 && buffer[1] == 0x4E && buffer[2] == 0x47
                            && buffer[3] == 0x0D && buffer[4] == 0x0A && buffer[5] == 0x1A && buffer[6] == 0x0A)//png match
                        {
                            await stream.ReadAsync(buffer, 0, 8);
                            rtn.Width = LittleEndianInt32(buffer, 0);
                            rtn.Height = LittleEndianInt32(buffer, 4);
                        }
                    }
                    else if (buffer[0] == 0xff) //jpeg/jfif 
                    {
                        read = await stream.ReadAsync(buffer, 0, 1);
                        if (read == 1 && buffer[0] == 0xd8)//jfif match SOI/FFD8
                        {
                            for (; ; )
                            {
                                await stream.ReadAsync(buffer, 0, 4);//mark + marksize
                                var chunksize = LittleEndianInt16(buffer, 2);
                                if (buffer[0] == 0xff && buffer[1] == 0xc0)
                                {
                                    await stream.ReadAsync(buffer, 0, 5);
                                    rtn.Height = LittleEndianInt16(buffer, 1);
                                    rtn.Width = LittleEndianInt16(buffer, 3);
                                    break;
                                }
                                var b = new byte[chunksize];
                                await stream.ReadAsync(b, 0, chunksize - 2);
                            }

                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    Debug.WriteLine(e.InnerException.Message);
                }
                finally
                {
                    client.CancelPendingRequests();
                    if(stream != null)
                        stream.Close();
                }
            }
            return rtn;
        }

    }
}