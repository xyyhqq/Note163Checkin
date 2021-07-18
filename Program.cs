﻿using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Note163Checkin
{
    class Program
    {
        static Conf _conf;
        static HttpClient _scClient;

        static async Task Main()
        {
            _conf = Deserialize<Conf>(GetEnvValue("CONF"));
            if (!string.IsNullOrWhiteSpace(_conf.ScKey))
            {
                _scClient = new HttpClient();
            }

           
            try
            {
                Console.WriteLine("有道云笔记签到开始运行...");
                await CheckInEveryDay(); 
                Console.WriteLine("签到运行完毕");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"出现异常，{ex.Message}");
                Console.WriteLine($"有道云笔记签到开始重新运行...");
                await CheckInEveryDay();
                Console.WriteLine("重新签到运行完毕");

            }
     
        }

        private static async Task CheckInEveryDay()
        {
            for (int i = 0; i < _conf.Users.Length; i++)
            {
                User user = _conf.Users[i];
                string title = $"账号 {i + 1}: {user.Task} ";
                Console.WriteLine($"共 {_conf.Users.Length} 个账号，正在运行{title}...");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");

                //登录账号
                var rspMsg = await client.PostAsync("https://note.youdao.com/login/acc/urs/verify/check?app=web&product=YNOTE&tp=urstoken&cf=6&fr=1&systemName=&deviceType=&ru=https%3A%2F%2Fnote.youdao.com%2FsignIn%2F%2FloginCallback.html&er=https%3A%2F%2Fnote.youdao.com%2FsignIn%2F%2FloginCallback.html&vcode=&systemName=Windows&deviceType=WindowsPC", new StringContent($"username={user.Username}&password={MD5Hash(user.Password)}", Encoding.UTF8, "application/x-www-form-urlencoded"));
                if (rspMsg.RequestMessage.RequestUri.AbsoluteUri.Contains("ecode"))
                {//登录失败
                    await Notify($"{title}登录失败，请检查账号密码是否正确！或者在网页上登录后再次运行本程序！", true);
                    continue;
                }

                //每日打开客户端（即登陆）
                string result = await (await client.PostAsync("https://note.youdao.com/yws/api/daupromotion?method=sync", null))
                    .Content.ReadAsStringAsync();
                if (result.Contains("error", StringComparison.OrdinalIgnoreCase))
                {//Cookie失效
                    await Notify($"{title}Cookie失效，请检查登录状态！", true);
                    continue;
                }

                long space = 0;
                space += Deserialize<YdNoteRsp>(result).RewardSpace;

                //签到
                result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=checkin", null))
                   .Content.ReadAsStringAsync();
                space += Deserialize<YdNoteRsp>(result).Space;

                //看广告
                for (int j = 0; j < 3; j++)
                {
                    result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adPrompt", null))
                       .Content.ReadAsStringAsync();
                    space += Deserialize<YdNoteRsp>(result).Space;
                }

                //看视频广告
                for (int j = 0; j < 3; j++)
                {
                    result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adRandomPrompt", null))
                       .Content.ReadAsStringAsync();
                    space += Deserialize<YdNoteRsp>(result).Space;
                }

                await Notify($"有道云笔记{title}签到成功，共获得空间 {space / 1048576} M");
            }
        }

        static string MD5Hash(string str)
        {
            StringBuilder sbHash = new StringBuilder(32);
            byte[] s = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
            for (int i = 0; i < s.Length; i++)
            {
                sbHash.Append(s[i].ToString("x2"));
            }
            return sbHash.ToString();
        }

        static async Task Notify(string msg, bool isFailed = false)
        {
            Console.WriteLine(msg);
            if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
            {
                await _scClient?.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?text={msg}");
            }
        }

        static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

        static string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);
    }

    #region Conf

    public class Conf
    {
        public User[] Users { get; set; }
        public string ScKey { get; set; }
        public string ScType { get; set; }
    }

    public class User
    {
        public string Task { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    #endregion

    class YdNoteRsp
    {
        /// <summary>
        /// Sync奖励空间
        /// </summary>
        public int RewardSpace { get; set; }

        /// <summary>
        /// 其他奖励空间
        /// </summary>
        public int Space { get; set; }
    }
}
