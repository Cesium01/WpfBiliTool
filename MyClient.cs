using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WpfBiliTool
{
    internal class MyClient
    {
        public const string uploadApi = "https://api.bilibili.com/x/dynamic/feed/draw/upload_bfs";
        public const string shortUrlApi = "http://api.bilibili.com/x/share/click";
        public const string dynamicDetailApi = "https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/get_dynamic_detail?dynamic_id=";
        public const string replyApi = "https://api.bilibili.com/x/v2/reply/main?jsonp=jsonp&mode=3&plat=1&type=";
        public const string repostApi = "https://api.vc.bilibili.com/dynamic_repost/v1/dynamic_repost/repost_detail?dynamic_id=";
        private static readonly string _s = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpClient client = new HttpClient(new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.All,
            CookieContainer = cookieContainer
        });

        public MyClient()
        {
            client.Timeout = TimeSpan.FromSeconds(18);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("DNT", "1");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip,deflate,br");
            client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        ~MyClient()
        {
            client.Dispose();
        }

        public async Task<string> UploadImage(FileStream fs)
        {
            MultipartFormDataContent data = new MultipartFormDataContent();
            string[] filePathSplit = fs.Name.Split('\\');
            data.Add(new StreamContent(fs), "file_up", filePathSplit[filePathSplit.Length - 1]);
            data.Add(new StringContent("dyn"), "biz");
            data.Add(new StringContent("daily"), "category");
            //TODO
            data.Add(new StringContent(GetCookie("bili_jct")), "csrf");
            HttpResponseMessage response = await client.PostAsync(uploadApi, data);
            response.EnsureSuccessStatusCode();
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync());
            return ((int)result["code"] == 0) && (result["data"] as JObject).ContainsKey("content")
                ? result["data"]["content"].ToString()
                : throw new Exception(result.ToString());
        }

        public async Task<string> GetShortUrl(string longUrl)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>()
            {
                ["build"] = "6500300",
                ["buvid"] = RandomBuvid(),
                ["oid"] = longUrl,
                ["platform"] = "android",
                ["share_channel"] = "COPY",
                ["share_id"] = "public.webview.0.0.pv",
                ["share_mode"] = "3"
            };
            FormUrlEncodedContent data = new FormUrlEncodedContent(dic);
            HttpResponseMessage response = await client.PostAsync(shortUrlApi, data);
            response.EnsureSuccessStatusCode();
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync());
            return ((int)result["code"] == 0) && (result["data"] as JObject).ContainsKey("content")
                ? result["data"]["content"].ToString()
                : throw new Exception(result.ToString());
        }

        public async Task<Dictionary<string,string>> DynamicRoll(string dynamicUrl, int mode)
        {
            client.DefaultRequestHeaders.Add("Referer", dynamicUrl);
            Dictionary<string, string> users = new Dictionary<string, string>();
            Match match = Regex.Match(dynamicUrl, @"t\.bilibili\.com/(\d+)");
            if(!match.Success)
            {
                throw new Exception("动态链接有误");
            }
            string dynamicId = match.Value.Replace("t.bilibili.com/", "");
            string oid = dynamicId;
            string type = "17";
            JToken detailData = await FetchApi(dynamicDetailApi + dynamicId);
            if ((int)detailData["card"]["desc"]["type"] == 2)
            {
                type = "11";
                oid = (string)detailData["card"]["desc"]["rid"];
            }
            string fixedReplyApi = replyApi + type + "&oid=" + oid;
            switch (mode)
            {
                case 0:
                    await FetchReply(users, fixedReplyApi, "0");
                    break;
                case 1:
                    await FetchRepost(users, dynamicId, "");
                    break;
                case 2:
                    await FetchReply(users, fixedReplyApi, "0");
                    await FetchRepost(users, dynamicId, "");
                    break;
                default:
                    break;
            }
            client.DefaultRequestHeaders.Remove("Referer");
            return users;
        }

        private async Task<JToken> FetchApi(string api)
        {
            var response = await client.GetAsync(api);
            response.EnsureSuccessStatusCode();
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync());
            if ((int)result["code"] != 0)
            {
                throw new Exception(result.ToString());
            }
            else
            {
                return result["data"];
            }
        }

        private async Task FetchReply(Dictionary<string, string> dic, string fixedApi, string _next)
        {
            long timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string api = fixedApi + "&next=" + _next + "&_=" + timeStamp;
            JToken data = await FetchApi(api);
            JToken cursor = data["cursor"];
            JToken replies = data["replies"];
            foreach(JToken reply in replies)
            {
                dic[reply["member"]["mid"].ToString()] = reply["member"]["uname"].ToString();
            }
            if (!(bool)cursor["is_end"])
            {
                await FetchReply(dic, fixedApi, (string)cursor["next"]);
            }
        }

        private async Task FetchRepost(Dictionary<string, string> dic, string dynamicId, string offset)
        {
            string api = offset == "" ? repostApi + dynamicId : repostApi + dynamicId + "&offset=" + offset;
            JToken data = await FetchApi(api);
            JToken reposts = data["items"];
            if (reposts != null)
            {
                foreach (JToken repost in reposts)
                {
                    dic[repost["desc"]["uid"].ToString()] = repost["desc"]["user_profile"]["info"]["uname"].ToString();
                }
            }
            if ((int)data["has_more"] == 1)
            {
                await FetchRepost(dic, dynamicId, (string)data["offset"]);
            }
        }

        public static string RandomBuvid()
        {
            StringBuilder sb = new StringBuilder();
            Random rd = new Random();
            for (int i = 0; i < 32; i++)
            {
                sb.Append(_s.Substring(rd.Next(62), 1));
            }
            return sb.ToString();
        }

        public string GetCookie(string name)
        {
            return cookieContainer.GetCookies(new Uri("https://www.bilibili.com/"))[name].Value;
        }
    }
}
