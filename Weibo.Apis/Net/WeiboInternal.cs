using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Weibo.DataModel;

namespace Weibo.Apis.Net
{
    public class WeiboInternal
    {
        internal static async Task<RestResult<TResult>>
            HttpsPost<TResult>(string path, List<KeyValuePair<string,string>> parameters) where TResult : class
        {
            var rtn = new RestResult<TResult>();
            using (var client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            }))
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(StringWithQualityHeaderValue.Parse("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(StringWithQualityHeaderValue.Parse("deflate"));
                var form = new FormUrlEncodedContent(parameters);

                var resp = await client.PostAsync(path, form);
                rtn.StatusCode = resp.StatusCode;
                rtn.Reason = resp.ReasonPhrase;
                if (resp.IsSuccessStatusCode)
                    rtn.Value = JsonConvert.DeserializeObject<TResult>(await resp.Content.ReadAsStringAsync());
                else if ((int)resp.StatusCode >= 400)
                {
                    var cont = await resp.Content.ReadAsStringAsync();
                    var er = JsonConvert.DeserializeObject<WeiboError>(cont);
                    rtn.Reason = er.Translate();
                }
            }
            return rtn;
        }

        internal static async Task<RestResult<TResult>> HttpsGet<TResult>(string url)
            where TResult : class
        {
            Debug.WriteLine(url);
            var rtn = new RestResult<TResult>();
            using (var client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            }))
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(StringWithQualityHeaderValue.Parse("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(StringWithQualityHeaderValue.Parse("deflate")); 
                for (var r = 0; r < 2; ++r)
                {
                    try
                    {
                        var resp = await client.GetAsync(url);
                        rtn.StatusCode = resp.StatusCode;
                        rtn.Reason = resp.ReasonPhrase;

                        if ((int) resp.StatusCode >= 500) //server error
                            continue; //retry
                        if (resp.IsSuccessStatusCode)
                            rtn.Value = JsonConvert.DeserializeObject<TResult>(await resp.Content.ReadAsStringAsync());
                        else if ((int) resp.StatusCode >= 400)
                        {
                            var er = JsonConvert.DeserializeObject<WeiboError>(await resp.Content.ReadAsStringAsync());
                            rtn.Reason = er.Translate();
                        }
                        //JToken.Parse(await resp.Content.ReadAsStringAsync());
                        break;
                    }
                    catch (HttpRequestException e)
                    {
//don't retry any request exception
                        rtn.StatusCode = HttpStatusCode.Unused;
                        rtn.Reason = e.Message;
                        if(e.InnerException != null)
                        {
                            rtn.Reason = e.InnerException.Message;
                        }
                        Debug.WriteLine(rtn.Reason);
                    }
                }
            }
            return rtn;
        }
    }
}