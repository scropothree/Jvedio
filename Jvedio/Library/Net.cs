﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using static Jvedio.StaticVariable;


namespace Jvedio
{


    public static class Net
    {
        public static int TCPTIMEOUT = 2;   // TCP 超时
        public static int HTTPTIMEOUT = 2; // HTTP 超时
        public static int ATTEMPTNUM = 2; // 最大尝试次数
        public static string UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36";

        public static int REQUESTTIMEOUT = 3000;//网站 HTML 获取超时
        public static int FILE_REQUESTTIMEOUT = 5000;//图片下载超时
        public static int READWRITETIMEOUT = 3000;


        public static void Init()
        {
            TCPTIMEOUT = Properties.Settings.Default.Timeout_tcp;
            HTTPTIMEOUT = Properties.Settings.Default.Timeout_forcestop;
            REQUESTTIMEOUT = Properties.Settings.Default.Timeout_http * 1000;
            FILE_REQUESTTIMEOUT = Properties.Settings.Default.Timeout_download * 1000;
            READWRITETIMEOUT = Properties.Settings.Default.Timeout_stream * 1000;
        }



        public enum HttpMode
        {
            Normal = 0,
            RedirectGet = 1
        }



        public static async Task<(string, int)> Http(string Url, string Method = "GET", HttpMode Mode = HttpMode.Normal, WebProxy Proxy = null, string Cookie = "")
        {
            string HtmlText = "";
            int StatusCode = 404;
            int num = 0;

            while (num < ATTEMPTNUM & string.IsNullOrEmpty(HtmlText))
            {
                try
                {
                    HtmlText = await Task.Run(() =>
                    {
                        string result = "";
                        HttpWebRequest Request;
                        HttpWebResponse Response = default;
                        try
                        {
                            Request = (HttpWebRequest)HttpWebRequest.Create(Url);
                            if (Cookie != "") Request.Headers.Add("Cookie", Cookie);
                            Request.Accept = "*/*";
                            Request.Timeout = REQUESTTIMEOUT;
                            Request.Method = Method;
                            Request.KeepAlive = false;
                            if (Mode == HttpMode.RedirectGet) Request.AllowAutoRedirect = false;
                            Request.Referer = Url;
                            Request.UserAgent = UserAgent;
                            Request.ReadWriteTimeout = READWRITETIMEOUT;
                            if (Proxy != null) Request.Proxy = Proxy;
                            Response = (HttpWebResponse)Request.GetResponse();



                            if (Response.StatusCode == HttpStatusCode.OK)
                            {
                                var SR = new StreamReader(Response.GetResponseStream());
                                result = SR.ReadToEnd();
                                SR.Close();
                                StatusCode = 200;
                            }
                            else if (Response.StatusCode == HttpStatusCode.Redirect && Mode == HttpMode.RedirectGet)
                            {
                                result = Response.Headers["Location"];// 获得 library 影片 Code
                            }
                            else { num = 2; }
                            Response.Close();
                        }
                        catch (WebException e)
                        {
                            Logger.LogN($"URL={Url},Message-{e.Message}");
                            if (e.Status == WebExceptionStatus.Timeout)
                                num += 1;
                            else
                                num = 2;
                        }
                        catch (Exception e)
                        {
                            Logger.LogN($"URL={Url},Message-{e.Message}");
                            num = 2;
                        }
                        finally
                        {
                            if (Response != null) Response.Close();
                        }

                        return result;
                    }).TimeoutAfter(TimeSpan.FromSeconds(HTTPTIMEOUT));

                }
                catch (TimeoutException ex) { Logger.LogN($"URL={Url},Message-{ex.Message}"); num = 2; }
            }

            return (HtmlText, StatusCode);
        }


        public async static Task<WebSite> CheckUrlType(string url)
        {
            WebSite webSite = WebSite.None;
            bool enablecookie = false;
            string label = "";
            (bool result, string title) = await Net.TestAndGetTitle(url, enablecookie, "", label);
            if (result)
            {
                //其他，进行判断
                if (title.ToLower().IndexOf("javbus") >= 0 && title.IndexOf("歐美") < 0)
                {
                    webSite = WebSite.Bus;
                }
                else if (title.ToLower().IndexOf("javbus") >= 0 && title.IndexOf("歐美") >= 0)
                {
                    webSite = WebSite.BusEu;
                }
                else if (title.ToLower().IndexOf("javlibrary") >= 0)
                {
                    webSite = WebSite.Library;
                }
                else if (title.ToLower().IndexOf("fanza") >= 0)
                {
                    webSite = WebSite.DMM;
                }
                else if (title.ToLower().IndexOf("jav321") >= 0)
                {
                    webSite = WebSite.Jav321;
                }
                else if (title.ToLower().IndexOf("javdb") >= 0)
                {
                    webSite = WebSite.DB;

                }
                else
                {
                    webSite = WebSite.None;
                }
            }
            return webSite;
        }


        public static async Task<(bool, string)> TestAndGetTitle(string Url, bool EnableCookie, string Cookie, string Label)
        {
            return await Task.Run(async () =>
            {
                bool result = false;
                string title = "";
                string content = ""; int statusCode = 404;
                if (EnableCookie)
                {
                    if (Label == "DB")
                    {
                        (content, statusCode) = await Http(Url + "v/P2Rz9", Proxy: null, Cookie: Cookie);
                        if (content != "")
                        {
                            if (content.IndexOf("FC2-659341") >= 0) result = true;
                            else result = false;
                        }
                    }
                }
                else
                {
                    (content, statusCode) = await Http(Url, Proxy: null);
                    if (content != "")
                    {
                        result = true;
                        //获得标题
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(content);
                        HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("//title");
                        if (titleNode != null)
                        {
                            title = titleNode.InnerText;
                        }

                    }
                }
                return (result, title);
            });
        }


        public static async Task<bool> TestUrl(string Url, bool EnableCookie, string Cookie, string Label)
        {
            return await Task.Run(async () =>
            {
                bool result = false;
                string content = ""; int statusCode = 404;
                if (EnableCookie)
                {
                    if (Label == "DB")
                    {
                        (content, statusCode) = await Http(Url + "v/P2Rz9", Proxy: null, Cookie: Cookie);
                        if (content != "")
                        {
                            if (content.IndexOf("FC2-659341") >= 0) result = true;
                            else result = false;
                        }
                    }
                }
                else
                {
                    (content, statusCode) = await Http(Url, Proxy: null);
                    if (content != "") result = true;
                }
                return result;
            });
        }




        private static bool IsDomainAlive(string aDomain, int aTimeoutSeconds)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(aDomain, 80, null, null);

                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(aTimeoutSeconds));

                    if (!success)
                    {
                        return false;
                    }

                    // we have connected
                    client.EndConnect(result);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                Logger.LogN($"URL={aDomain},Message-{e.Message}");
            }
            return false;
        }
        public static (byte[], string cookies) DownLoadFile(string Url, WebProxy Proxy = null, string Host = "", string SetCookie = "")
        {
            if (Url.IndexOf("fc2club.com") < 0)
                if (!IsDomainAlive(new Uri(Url).Host, TCPTIMEOUT)) { Logger.LogN($"URL={Url},Message-Tcp连接超时"); return (null, ""); }
            Util.SetCertificatePolicy();
            byte[] ImageByte = null;
            string Cookies = SetCookie;
            int num = 0;
            while (num < ATTEMPTNUM && ImageByte == null)
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest Request;
                var Response = default(HttpWebResponse);
                try
                {
                    Request = (HttpWebRequest)HttpWebRequest.Create(Url);
                    if (Host != "") Request.Host = Host;
                    if (Proxy != null) Request.Proxy = Proxy;
                    if (SetCookie != "") Request.Headers.Add("Cookie", SetCookie);
                    Request.Accept = "*/*";
                    Request.Timeout = FILE_REQUESTTIMEOUT;
                    Request.Method = "GET";
                    Request.KeepAlive = false;
                    Request.Referer = Url;
                    Request.UserAgent = UserAgent;
                    Request.ReadWriteTimeout = READWRITETIMEOUT;
                    Response = (HttpWebResponse)Request.GetResponse();
                    if (Response.StatusCode == HttpStatusCode.OK)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            Response.GetResponseStream().CopyTo(ms);
                            ImageByte = ms.ToArray();
                        };
                        //获得 app_uid
                        WebHeaderCollection Headers = Response.Headers;
                        if (Headers != null & SetCookie == "")
                        {
                            if (Headers["Set-Cookie"] != null) Cookies = Headers["Set-Cookie"].Split(';')[0];
                            Console.WriteLine(Cookies);
                        }
                    }
                    else
                    {
                        num = 2;
                    }
                    Response.Close();
                }
                catch (WebException e)
                {
                    Logger.LogN($"URL={Url},Message-{e.Message}");
                    if (e.Status == WebExceptionStatus.Timeout) { num += 1; } else { num = 2; }
                }
                catch (Exception e)
                {
                    Logger.LogE(e);
                    num = 2;
                }
                finally
                {
                    if (Response != null) Response.Close();
                }
            }
            return (ImageByte, Cookies);
        }

        public static async Task<(bool, string)> DownLoadImage(string Url, ImageType imageType, string ID, string Cookie = "")
        {
            if (Url.IndexOf('/') < 0) { return (false, ""); }
            bool result = false; string cookies = Cookie;
            byte[] ImageBytes = null;
            (ImageBytes, cookies) = await Task.Run(() =>
            {
                string Host = "";
                if (Url.IndexOf("pics.dmm") >= 0) { Host = "pics.dmm.co.jp"; }
                else if (Url.IndexOf("pics.javcdn.pw") >= 0)
                { Host = "pics.javcdn.pw"; }
                else if (Url.IndexOf("images.javcdn.pw") >= 0) { Host = "images.javcdn.pw"; }

                //if (Url.IndexOf("jdbimgs") >= 0) cookies = AllCookies.DB;

                //if (imageType == ImageType.ExtraImage)
                //{
                (ImageBytes, cookies) = DownLoadFile(Url, Host: Host, SetCookie: cookies);
                //}
                //else { (ImageBytes, cookies) = DownLoadFile(Url, Host: Host, SetCookie: Cookie); }
                return (ImageBytes, cookies);
            });


            if (ImageBytes == null) { result = false; }
            else
            {
                result = true;
                StaticClass.SaveImage(ID, ImageBytes, imageType, Url);
            }
            return (result, cookies);
        }



        public static async Task<bool> DownActress(string ID, string Name)
        {
            bool result = false;
            string Url = RootUrl.Bus + $"star/{ID}";
            string Content; int StatusCode; string ResultMessage;
            (Content, StatusCode) = await Http(Url);
            if (StatusCode == 200 && Content != "")
            {
                //id搜索
                BusParse busParse = new BusParse(ID, Content, VedioType.骑兵);
                Actress actress = busParse.ParseActress();
                if (actress.birthday == "" && actress.age == 0 && actress.birthplace == "")
                { Console.WriteLine($"该网址无演员信息：{Url}"); ResultMessage = "该网址无演员信息=>Bus"; Logger.LogN($"URL={Url},Message-{ResultMessage}"); }
                else
                {
                    actress.sourceurl = Url;
                    actress.source = "javbus";
                    actress.id = ID;
                    actress.name = Name;
                    //保存信息
                    DataBase.InsertActress(actress);

                    result = true;
                }
            }
            else { Console.WriteLine($"无法访问 404：{Url}"); ResultMessage = "无法访问=>Bus"; Logger.LogN($"URL={Url},Message-{ResultMessage}"); }
            return result;
        }


        public async static Task<(bool, string)> DownLoadSmallPic(DetailMovie dm)
        {
            //不存在才下载
            if (!File.Exists(StaticVariable.BasePicPath + $"SmallPic\\{dm.id}.jpg"))
            {
                Console.WriteLine("开始下载小图");
                Console.WriteLine(dm.source);
                if (dm.source == "javdb") return (false, "");
                else return await Net.DownLoadImage(dm.smallimageurl, ImageType.SmallImage, dm.id);
            }
            else return (false, "");

        }


        public async static Task<(bool, string)> DownLoadBigPic(DetailMovie dm)
        {
            if (!File.Exists(StaticVariable.BasePicPath + $"BigPic\\{dm.id}.jpg"))
            {
                return await Net.DownLoadImage(dm.bigimageurl, ImageType.BigImage, dm.id);
            }
            else
            {
                return (false, "");
            }
        }

        public static async  Task<bool> ParseSpecifiedInfo(WebSite webSite,string id,string url)
        {
            string content = "";
            int StatusCode = 404;
            if (webSite==WebSite.DB) (content, StatusCode) = await Net.Http(url, Cookie: Properties.Settings.Default.DBCookie);
            else ( content,  StatusCode) = await Net.Http(url);

            if(StatusCode!=200 || content == "")
            {
                return false;
            }
            else
            {
                Dictionary<string, string> Info = new Dictionary<string, string>();
                Info.Add("sourceurl", url);
                if (webSite == WebSite.Bus)
                {
                    Info = new BusParse(id, content,Identify.GetVedioType(id)).Parse();
                    Info.Add("source", "javbus");
                }
                else if (webSite == WebSite.BusEu)
                {
                    Info = new BusParse(id, content, VedioType.欧美).Parse();
                    Info.Add("source", "javbus");
                }
                else if (webSite == WebSite.DB)
                {
                    Info = new JavDBParse(id, content,url.Split('/').Last()).Parse();
                    Info.Add("source", "javdb");
                }
                else if (webSite == WebSite.Library)
                {
                    Info = new LibraryParse(id, content).Parse();
                    Info.Add("source", "javlibrary");
                }
                if (Info.Count > 2)
                {
                    //保存信息
                    Info["id"] = id;
                    DataBase.UpdateInfoFromNet(Info, webSite);
                    DetailMovie detailMovie = DataBase.SelectDetailMovieById(id);


                    //nfo 信息保存到视频同目录
                    if (Properties.Settings.Default.SaveInfoToNFO)
                    {
                        if (Directory.Exists(Properties.Settings.Default.NFOSavePath))
                        {
                            //固定位置
                            StaticClass.SaveToNFO(detailMovie, Path.Combine(Properties.Settings.Default.NFOSavePath, $"{id}.nfo"));
                        }
                        else
                        {
                            //与视频同路径
                            string path = detailMovie.filepath;
                            if (System.IO.File.Exists(path))
                            {
                                StaticClass.SaveToNFO(detailMovie, Path.Combine(new FileInfo(path).DirectoryName, $"{id}.nfo"));
                            }
                        }
                    }
                    return true;
                }

            }

            return false;



        }




        public static async Task<(bool, string)> DownLoadFromNet(Movie movie)
        {
            bool success = false;
            string message = "";
            Movie newMovie;
            if (movie.vediotype == (int)VedioType.欧美)
            {
                if (!string.IsNullOrEmpty( RootUrl.BusEu) && EnableUrl.BusEu) await new BusCrawler(movie.id, (VedioType)movie.vediotype).Crawl((statuscode)=> { message = statuscode.ToString(); } );
                else if (!string.IsNullOrEmpty(RootUrl.BusEu)) message = "未开启欧美网址";
                else message = "网址未正确配置";
            }
            else
            {
                if (movie.id.ToUpper().IndexOf("FC2") >= 0)
                {
                        if (!string.IsNullOrEmpty(RootUrl.DB) && EnableUrl.DB) {  await new DBCrawler(movie.id).Crawl((statuscode) => { message = statuscode.ToString(); }, (statuscode) => { message = statuscode.ToString(); }); }
                        else if (!string.IsNullOrEmpty(RootUrl.DB)) message = "未开启 DB 网址";
                    else message = "网址未正确配置";
                }
                else
                {
                    if (!string.IsNullOrEmpty(RootUrl.Bus) && EnableUrl.Bus) await new BusCrawler(movie.id, (VedioType)movie.vediotype).Crawl((statuscode) => { message = statuscode.ToString(); });
                    else if (!string.IsNullOrEmpty(RootUrl.Bus)) message = "未开启 Bus 网址";
                    else message = "网址未正确配置";

                    newMovie = DataBase.SelectMovieByID(movie.id);
                    if (newMovie != null && (newMovie.title=="" || newMovie.smallimageurl=="" || newMovie.bigimageurl=="" || newMovie.extraimageurl=="" ) )
                    {
                        if (!string.IsNullOrEmpty(RootUrl.Library) && EnableUrl.Library) { await new LibraryCrawler(movie.id).Crawl((statuscode) => { message = statuscode.ToString(); },(statuscode) => { message = statuscode.ToString(); }); }
                        else if(!string.IsNullOrEmpty(RootUrl.Library)) message = "未开启 Library 网址";
                        else message = "网址未正确配置";
                    }

                   

                    newMovie = DataBase.SelectMovieByID(movie.id);
                    if (newMovie != null && (newMovie.title == "" || newMovie.smallimageurl == "" || newMovie.bigimageurl == "" || newMovie.extraimageurl == ""))
                    {
                        if (!string.IsNullOrEmpty(RootUrl.DB) && EnableUrl.DB)  await new DBCrawler(movie.id).Crawl((statuscode) => { message = statuscode.ToString(); },(statuscode) => { message = statuscode.ToString(); });
                        else if (!string.IsNullOrEmpty(RootUrl.DB)) message = "未开启 DB 网址";
                        else message = "网址未正确配置";
                    }
                        

                }

            }

            newMovie = DataBase.SelectMovieByID(movie.id);
            if (newMovie != null )
            {
                if (newMovie.title != "")
                    success = true;
                else
                    success = false;
            }
            else
                success = false;


            return (success,message);

        }

    }









    public static class Util
    {
        /// <summary>
        /// Sets the cert policy.
        /// </summary>
        public static void SetCertificatePolicy()
        {
            ServicePointManager.ServerCertificateValidationCallback
                       += RemoteCertificateValidate;
        }

        /// <summary>
        /// Remotes the certificate validate.
        /// </summary>
        private static bool RemoteCertificateValidate(
           object sender, X509Certificate cert,
            X509Chain chain, SslPolicyErrors error)
        {
            // trust any certificate!!!
            //System.Console.WriteLine("Warning, trust any certificate");
            return true;
        }
    }



}
